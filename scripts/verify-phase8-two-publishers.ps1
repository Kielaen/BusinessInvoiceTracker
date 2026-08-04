[CmdletBinding()]
param(
    [string]$DatabaseContainer = 'business-invoice-tracker-postgres-1',
    [string]$PrimaryApiContainer = 'business-invoice-tracker-api-1',
    [string]$ReplicaApiContainer = 'business-invoice-tracker-api-replica-1',
    [int]$MessageCount = 100
)

$ErrorActionPreference = 'Stop'

# The proof requires two publishers running the same current image. Recreate both
# app/sidecar pairs so an old local replica cannot claim events with stale routing.
docker compose --env-file .env.example --profile replica-test up -d --build `
    api api-dapr api-replica api-replica-dapr --wait | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Could not recreate the two current publisher replicas.' }

$marker = "phase8-two-publisher-$([guid]::NewGuid())"
$since = (Get-Date).ToUniversalTime().ToString('o')
$sql = @"
WITH batch AS (
  SELECT gen_random_uuid() AS id, gen_random_uuid() AS probe_id
  FROM generate_series(1, $MessageCount)
)
INSERT INTO outbox_messages
  ("Id", "Type", "Payload", "OccurredAtUtc", "PublishAttempts", "NextAttemptAtUtc")
SELECT id,
  'WalkingSkeletonEvent',
  jsonb_build_object(
    'messageId', id,
    'probeId', probe_id,
    'requestedBy', '$marker',
    'occurredAtUtc', now()),
  now(),
  0,
  now()
FROM batch;
"@
$null = $sql | docker exec -i $DatabaseContainer psql -U invoice -d invoice -v ON_ERROR_STOP=1
if ($LASTEXITCODE -ne 0) { throw 'Could not insert the publisher concurrency batch.' }

$deadline = (Get-Date).AddSeconds(60)
do {
    Start-Sleep -Milliseconds 250
    $evidenceSql = @"
SELECT json_build_object(
  'outboxRows', count(*),
  'publishedRows', count(*) FILTER (WHERE to_jsonb(o)->>'PublishedAtUtc' IS NOT NULL),
  'singleAttemptRows', count(*) FILTER (WHERE (to_jsonb(o)->>'PublishAttempts')::int = 1),
  'maxAttempts', max((to_jsonb(o)->>'PublishAttempts')::int),
  'receipts', (SELECT count(*) FROM automation_receipts r WHERE to_jsonb(r)->>'RequestedBy' = '$marker'))
FROM outbox_messages o
WHERE to_jsonb(o)->>'Payload' LIKE '%$marker%';
"@
    $json = $evidenceSql | docker exec -i $DatabaseContainer `
        psql -U invoice -d invoice -tA
    $evidence = $json.Trim() | ConvertFrom-Json
} until (($evidence.publishedRows -eq $MessageCount -and
    $evidence.receipts -eq $MessageCount) -or (Get-Date) -ge $deadline)

if ($evidence.outboxRows -ne $MessageCount -or
    $evidence.publishedRows -ne $MessageCount -or
    $evidence.singleAttemptRows -ne $MessageCount -or
    $evidence.maxAttempts -ne 1 -or
    $evidence.receipts -ne $MessageCount) {
    throw "Publisher lease evidence is inconsistent: $json"
}

$primaryLogs = docker logs --since $since $PrimaryApiContainer 2>&1
$replicaLogs = docker logs --since $since $ReplicaApiContainer 2>&1
$primaryPublished = @($primaryLogs | Select-String 'Published outbox message').Count
$replicaPublished = @($replicaLogs | Select-String 'Published outbox message').Count
if ($primaryPublished -lt 1 -or $replicaPublished -lt 1) {
    throw "Both publishers did not claim work: primary=$primaryPublished replica=$replicaPublished"
}

[pscustomobject]@{
    phase=8
    marker=$marker
    inserted=$evidence.outboxRows
    published=$evidence.publishedRows
    exactlyOneAttempt=$evidence.singleAttemptRows
    maxAttempts=$evidence.maxAttempts
    inboxSideEffects=$evidence.receipts
    primaryPublisherLogCount=$primaryPublished
    replicaPublisherLogCount=$replicaPublished
} | ConvertTo-Json -Depth 4
