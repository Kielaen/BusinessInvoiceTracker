[CmdletBinding()]
param(
    [switch]$Build,
    [switch]$Observability,
    [switch]$Cleanup
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$composeArguments = @('compose', '--env-file', '.env.example', '-f', 'compose.yaml')
if ($Observability) {
    $composeArguments += @(
        '-f',
        'compose.observability.yaml',
        '--profile',
        'observability'
    )
}

function Invoke-Compose {
    & docker @composeArguments @args
    if ($LASTEXITCODE -ne 0) {
        throw "docker compose failed with exit code $LASTEXITCODE"
    }
}

function Wait-ForHealthyContainer([string]$containerName, [int]$seconds = 120) {
    $deadline = (Get-Date).AddSeconds($seconds)
    do {
        $health = docker inspect --format '{{.State.Health.Status}}' $containerName 2>$null
        if ($health -eq 'healthy') {
            return
        }
        Start-Sleep -Seconds 2
    } while ((Get-Date) -lt $deadline)

    throw "$containerName did not become healthy within $seconds seconds."
}

Push-Location $repositoryRoot
try {
    $upArguments = @('up', '--detach')
    if ($Build) {
        $upArguments += '--build'
    }
    Invoke-Compose @upArguments

    Wait-ForHealthyContainer 'business-invoice-tracker-postgres-1'
    Wait-ForHealthyContainer 'business-invoice-tracker-api-1'
    Wait-ForHealthyContainer 'business-invoice-tracker-automation-1'

    $session = New-Object Microsoft.PowerShell.Commands.WebRequestSession
    $anonymousStatus = 0
    try {
        Invoke-RestMethod 'http://127.0.0.1:8080/api/v1/auth/me' `
            -WebSession $session | Out-Null
        $anonymousStatus = 200
    }
    catch {
        $anonymousStatus = [int]$_.Exception.Response.StatusCode
    }
    if ($anonymousStatus -ne 401) {
        throw "Anonymous authentication check returned $anonymousStatus instead of 401."
    }

    $anonymousToken = Invoke-RestMethod `
        'http://127.0.0.1:8080/api/v1/auth/antiforgery' -WebSession $session
    $anonymousHeaders = @{}
    $anonymousHeaders[$anonymousToken.headerName] = $anonymousToken.token
    $credentials = @{
        email = 'owner@acme.test'
        password = 'Phase2-local-only!'
    } | ConvertTo-Json
    $login = Invoke-RestMethod 'http://127.0.0.1:8080/api/v1/auth/login' `
        -Method Post -WebSession $session -Headers $anonymousHeaders `
        -ContentType 'application/json' -Body $credentials
    if ($login.email -ne 'owner@acme.test') {
        throw 'Seeded local-reviewer authentication did not return the expected user.'
    }

    $authenticatedToken = Invoke-RestMethod `
        'http://127.0.0.1:8080/api/v1/auth/antiforgery' -WebSession $session
    $authenticatedHeaders = @{}
    $authenticatedHeaders[$authenticatedToken.headerName] = `
        $authenticatedToken.token
    $accepted = Invoke-RestMethod `
        'http://127.0.0.1:8080/api/v1/walking-skeleton' `
        -Method Post -WebSession $session -Headers $authenticatedHeaders

    $statusUrl = "http://127.0.0.1:8080/api/v1/walking-skeleton/$($accepted.probeId)"
    $deadline = (Get-Date).AddSeconds(45)
    do {
        $status = Invoke-RestMethod $statusUrl -WebSession $session
        if ($status.status -eq 'processed') {
            break
        }
        Start-Sleep -Milliseconds 250
    } while ((Get-Date) -lt $deadline)
    if ($status.status -ne 'processed') {
        throw "Probe $($accepted.probeId) was not processed within 45 seconds."
    }

    $duplicatePayload = @{
        id = [guid]::NewGuid().ToString()
        source = 'phase-1-verifier'
        specVersion = '1.0'
        type = 'com.dapr.event.sent'
        traceParent = $null
        data = @{
            messageId = $accepted.messageId
            probeId = $accepted.probeId
            requestedBy = 'owner@acme.test'
            occurredAtUtc = (Get-Date).ToUniversalTime().ToString('o')
        }
    } | ConvertTo-Json
    1..2 | ForEach-Object {
        $duplicateResponse = Invoke-WebRequest `
            'http://127.0.0.1:5191/events/walking-skeleton' `
            -Method Post -ContentType 'application/json' `
            -Body $duplicatePayload -UseBasicParsing
        if ($duplicateResponse.StatusCode -ne 204) {
            throw "Duplicate delivery returned $($duplicateResponse.StatusCode)."
        }
    }

    $expectedUsers = @{
        api = '1654'
        automation = '1654'
        web = '101'
    }
    foreach ($entry in $expectedUsers.GetEnumerator()) {
        $containerId = Invoke-Compose ps --quiet $entry.Key
        $actualUser = docker inspect --format '{{.Config.User}}' $containerId
        if ($actualUser -ne $entry.Value) {
            throw "$($entry.Key) runs as '$actualUser'; expected '$($entry.Value)'."
        }
    }

    [pscustomobject]@{
        anonymousStatus = $anonymousStatus
        user = $login.email
        probeId = $accepted.probeId
        messageId = $accepted.messageId
        finalStatus = $status.status
        processedAtUtc = $status.processedAtUtc
        containerUsers = $expectedUsers
    } | ConvertTo-Json -Depth 5

    if ($Observability) {
        $telemetryDeadline = (Get-Date).AddSeconds(90)
        do {
            try {
                $resources = Invoke-RestMethod `
                    'http://127.0.0.1:18888/api/telemetry/resources'
                $completeResources = @(
                    $resources | Where-Object {
                        $_.name -in @('invoice-api', 'invoice-automation') -and
                        $_.hasLogs -and
                        $_.hasTraces -and
                        $_.hasMetrics
                    }
                )
                if ($completeResources.Count -eq 2) {
                    $completeResources |
                        Select-Object name, hasLogs, hasTraces, hasMetrics |
                        ConvertTo-Json
                    break
                }
            }
            catch {
                # Export and dashboard startup are asynchronous.
            }
            Start-Sleep -Seconds 1
        } while ((Get-Date) -lt $telemetryDeadline)

        if ($completeResources.Count -ne 2) {
            throw 'The optional dashboard did not receive all three signals from both services.'
        }
    }
}
finally {
    if ($Cleanup) {
        Invoke-Compose down --volumes --remove-orphans
    }
    Pop-Location
}
