[CmdletBinding()]
param(
    [ValidatePattern('^[a-z0-9][a-z0-9_-]+$')]
    [string]$ProjectName = 'business-invoice-tracker',
    [int]$MessageCount = 12
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$databaseContainer = "$ProjectName-postgres-1"
$apiContainer = "$ProjectName-api-1"
$redisContainer = "$ProjectName-redis-1"
$marker = "phase11-shutdown-$([guid]::NewGuid().ToString('N'))"

function Invoke-Compose {
    docker compose --project-name $ProjectName --env-file .env.example @args
    if ($LASTEXITCODE -ne 0) {
        throw "docker compose $($args -join ' ') failed with exit code $LASTEXITCODE."
    }
}

function Wait-Healthy([string]$container, [int]$seconds = 120) {
    $deadline = (Get-Date).AddSeconds($seconds)
    do {
        $health = docker inspect --format '{{.State.Health.Status}}' $container 2>$null
        if ($health -eq 'healthy') { return }
        Start-Sleep -Seconds 2
    } while ((Get-Date) -lt $deadline)
    throw "$container did not become healthy."
}

function Get-Evidence {
    $sql = @"
SELECT json_build_object(
  'rows', count(*),
  'published', count(*) FILTER (WHERE "PublishedAtUtc" IS NOT NULL),
  'attempted', count(*) FILTER (WHERE "PublishAttempts" > 0),
  'maxAttempts', coalesce(max("PublishAttempts"), 0),
  'receipts', (SELECT count(*) FROM automation_receipts
    WHERE "RequestedBy" = '$marker'))
FROM outbox_messages
WHERE "Payload"->>'requestedBy' = '$marker';
"@
    $json = $sql | docker exec -i $databaseContainer `
        psql -v ON_ERROR_STOP=1 -U invoice -d invoice -At
    if ($LASTEXITCODE -ne 0) { throw 'Shutdown evidence query failed.' }
    return $json.Trim() | ConvertFrom-Json
}

Push-Location $repositoryRoot
try {
    Invoke-Compose up --detach
    Wait-Healthy $apiContainer
    Wait-Healthy $redisContainer

    Invoke-Compose stop redis
    $sql = @"
WITH batch AS (
  SELECT gen_random_uuid() AS id, gen_random_uuid() AS probe_id
  FROM generate_series(1, $MessageCount)
)
INSERT INTO outbox_messages
  ("Id", "Type", "Payload", "OccurredAtUtc", "PublishAttempts", "NextAttemptAtUtc")
SELECT id, 'WalkingSkeletonEvent',
  jsonb_build_object(
    'messageId', id,
    'probeId', probe_id,
    'requestedBy', '$marker',
    'occurredAtUtc', now()),
  now(), 0, now()
FROM batch;
"@
    $null = $sql | docker exec -i $databaseContainer `
        psql -v ON_ERROR_STOP=1 -U invoice -d invoice
    if ($LASTEXITCODE -ne 0) { throw 'Could not insert shutdown backlog.' }

    $deadline = (Get-Date).AddSeconds(30)
    do {
        Start-Sleep -Milliseconds 250
        $beforeStop = Get-Evidence
    } until ($beforeStop.attempted -gt 0 -or (Get-Date) -ge $deadline)
    if ($beforeStop.rows -ne $MessageCount -or $beforeStop.attempted -lt 1 -or
        $beforeStop.published -ne 0) {
        throw "Could not establish an in-flight durable failure backlog: $($beforeStop | ConvertTo-Json -Compress)"
    }

    $stopStarted = Get-Date
    Invoke-Compose stop --timeout 15 api-dapr api
    $stopSeconds = ((Get-Date) - $stopStarted).TotalSeconds
    $exitCode = docker inspect --format '{{.State.ExitCode}}' $apiContainer
    if ($LASTEXITCODE -ne 0) { throw 'Could not inspect stopped API.' }
    if ([int]$exitCode -notin @(0, 143)) {
        throw "API exited unexpectedly during graceful stop: $exitCode."
    }
    $duringStop = Get-Evidence
    if ($duringStop.rows -ne $MessageCount -or $duringStop.published -ne 0) {
        throw 'Graceful stop lost or falsely published durable backlog rows.'
    }

    Invoke-Compose start redis
    Wait-Healthy $redisContainer
    Invoke-Compose start api
    Invoke-Compose start api-dapr
    Wait-Healthy $apiContainer

    $deadline = (Get-Date).AddSeconds(90)
    do {
        Start-Sleep -Milliseconds 500
        $recovered = Get-Evidence
    } until (($recovered.published -eq $MessageCount -and
        $recovered.receipts -eq $MessageCount) -or (Get-Date) -ge $deadline)
    if ($recovered.rows -ne $MessageCount -or
        $recovered.published -ne $MessageCount -or
        $recovered.receipts -ne $MessageCount -or
        $recovered.maxAttempts -gt 4) {
        throw "Graceful shutdown recovery is inconsistent: $($recovered | ConvertTo-Json -Compress)"
    }

    Write-Output "Shutdown marker=$marker stopSeconds=$([Math]::Round($stopSeconds,2)) exit=$exitCode attemptedBefore=$($beforeStop.attempted) recovered=$($recovered.published) receipts=$($recovered.receipts) maxAttempts=$($recovered.maxAttempts)"
    Write-Output 'PASS Phase 11 graceful shutdown and durable retry verification'
}
finally {
    # Restoration is idempotent and preserves retained volumes.
    Invoke-Compose start redis
    Wait-Healthy $redisContainer
    Invoke-Compose start api
    Invoke-Compose start api-dapr
    Wait-Healthy $apiContainer
    Pop-Location
}
