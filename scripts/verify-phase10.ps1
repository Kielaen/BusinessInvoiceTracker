[CmdletBinding()]
param(
    [string]$ProjectName = 'business-invoice-tracker'
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$composeArguments = @(
    'compose', '--project-name', $ProjectName,
    '--env-file', '.env.example', '-f', 'compose.yaml')
$apiContainer = "$ProjectName-api-1"
$automationContainer = "$ProjectName-automation-1"
$postgresContainer = "$ProjectName-postgres-1"
$redisContainer = "$ProjectName-redis-1"
$mailpitContainer = "$ProjectName-mailpit-1"
$results = [ordered]@{}

function Invoke-Compose {
    & docker @composeArguments @args
    if ($LASTEXITCODE -ne 0) {
        throw "docker compose failed with exit code $LASTEXITCODE"
    }
}

function Get-HttpStatus([string]$uri) {
    try {
        return [int](Invoke-WebRequest $uri -UseBasicParsing -TimeoutSec 10).StatusCode
    }
    catch {
        if ($null -eq $_.Exception.Response) { throw }
        return [int]$_.Exception.Response.StatusCode
    }
}

function Wait-HttpStatus(
    [string]$uri,
    [int]$expected,
    [int]$seconds = 45) {
    $deadline = (Get-Date).AddSeconds($seconds)
    do {
        try { $actual = Get-HttpStatus $uri } catch { $actual = 0 }
        if ($actual -eq $expected) { return }
        Start-Sleep -Milliseconds 500
    } while ((Get-Date) -lt $deadline)
    throw "$uri returned $actual instead of $expected within $seconds seconds."
}

function Wait-ContainerHealthy([string]$name, [int]$seconds = 90) {
    $deadline = (Get-Date).AddSeconds($seconds)
    do {
        $health = docker inspect --format '{{.State.Health.Status}}' $name 2>$null
        if ($health -eq 'healthy') { return }
        Start-Sleep -Seconds 1
    } while ((Get-Date) -lt $deadline)
    throw "$name did not become healthy within $seconds seconds."
}

function Assert-LiveReadyFailure(
    [string]$liveUri,
    [string]$readyUri,
    [string]$dependency) {
    Wait-HttpStatus $liveUri 200 20
    Wait-HttpStatus $readyUri 503 20
    $results[$dependency] = [ordered]@{ liveness = 200; readiness = 503 }
}

function New-AuthenticatedSession {
    $session = New-Object Microsoft.PowerShell.Commands.WebRequestSession
    $token = Invoke-RestMethod `
        'http://127.0.0.1:8080/api/v1/auth/antiforgery' -WebSession $session
    $headers = @{}; $headers[$token.headerName] = $token.token
    $null = Invoke-RestMethod 'http://127.0.0.1:8080/api/v1/auth/login' `
        -Method Post -WebSession $session -Headers $headers `
        -ContentType 'application/json' -Body (@{
            email = 'owner@acme.test'; password = 'Phase2-local-only!'
        } | ConvertTo-Json)
    $token = Invoke-RestMethod `
        'http://127.0.0.1:8080/api/v1/auth/antiforgery' -WebSession $session
    $headers = @{}; $headers[$token.headerName] = $token.token
    return [pscustomobject]@{ Session = $session; Headers = $headers }
}

Push-Location $repositoryRoot
try {
    Invoke-Compose up --detach
    Wait-ContainerHealthy $apiContainer
    Wait-ContainerHealthy $automationContainer
    Wait-HttpStatus 'http://127.0.0.1:8080/health/ready' 200
    Wait-HttpStatus 'http://127.0.0.1:5191/health/ready' 200
    $baselineSnapshot = Invoke-RestMethod `
        'http://127.0.0.1:5191/internal/operations/status'
    if ($baselineSnapshot.isStale) { throw 'Baseline operator snapshot is stale.' }
    $results.baseline = [ordered]@{
        apiReady = 200
        automationReady = 200
        snapshot = $baselineSnapshot
    }

    Invoke-Compose stop api-dapr
    Assert-LiveReadyFailure `
        'http://127.0.0.1:8080/health/live' `
        'http://127.0.0.1:8080/health/ready' 'apiDapr'
    Invoke-Compose start api-dapr
    Wait-HttpStatus 'http://127.0.0.1:8080/health/ready' 200 60

    Invoke-Compose stop automation-dapr
    Assert-LiveReadyFailure `
        'http://127.0.0.1:5191/health/live' `
        'http://127.0.0.1:5191/health/ready' 'automationDapr'
    Invoke-Compose start automation-dapr
    Wait-HttpStatus 'http://127.0.0.1:5191/health/ready' 200 60

    Invoke-Compose stop api-dapr api
    Wait-HttpStatus 'http://127.0.0.1:8080/' 200 20
    Wait-HttpStatus 'http://127.0.0.1:8080/api/v1/auth/me' 502 20
    $results.apiProcess = [ordered]@{
        staticWeb = 200
        apiBoundary = 502
    }
    Invoke-Compose start api api-dapr
    Wait-HttpStatus 'http://127.0.0.1:8080/health/ready' 200 60

    Invoke-Compose stop automation-dapr automation
    Wait-HttpStatus 'http://127.0.0.1:8080/health/ready' 200 20
    $automationUnavailable = $false
    try {
        $null = Invoke-WebRequest 'http://127.0.0.1:5191/health/live' `
            -UseBasicParsing -TimeoutSec 3
    }
    catch {
        $automationUnavailable = $true
    }
    if (-not $automationUnavailable) {
        throw 'Automation process remained reachable after it was stopped.'
    }
    $results.automationProcess = [ordered]@{
        apiReady = 200
        automationUnavailable = $true
    }
    Invoke-Compose start automation automation-dapr
    Wait-HttpStatus 'http://127.0.0.1:5191/health/ready' 200 60

    Invoke-Compose stop web
    $webUnavailable = $false
    try {
        $null = Invoke-WebRequest 'http://127.0.0.1:8080/' `
            -UseBasicParsing -TimeoutSec 3
    }
    catch {
        $webUnavailable = $true
    }
    if (-not $webUnavailable) { throw 'Web process remained reachable after stop.' }
    Invoke-Compose start web
    Wait-HttpStatus 'http://127.0.0.1:8080/' 200 30
    $results.webProcess = [ordered]@{
        unavailableDuringStop = $true
        recoveredStatus = 200
    }

    foreach ($controlPlane in @('placement', 'scheduler')) {
        Invoke-Compose stop $controlPlane
        Assert-LiveReadyFailure `
            'http://127.0.0.1:5191/health/live' `
            'http://127.0.0.1:5191/health/ready' $controlPlane
        Invoke-Compose start $controlPlane
        Wait-HttpStatus 'http://127.0.0.1:5191/health/ready' 200 60
    }

    Invoke-Compose stop mailpit
    Wait-HttpStatus 'http://127.0.0.1:5191/health/live' 200 20
    Wait-HttpStatus 'http://127.0.0.1:5191/health/ready' 200 20
    $degraded = Invoke-RestMethod 'http://127.0.0.1:5191/health/ready'
    if ($degraded.status -ne 'degraded' -or
        (@($degraded.checks | Where-Object name -eq 'mailpit')[0].status -ne 'unhealthy')) {
        throw "Mailpit outage was not reported as degraded: $($degraded | ConvertTo-Json -Compress)"
    }
    $results.mailpit = [ordered]@{ liveness = 200; readiness = 200; status = 'degraded' }
    Invoke-Compose start mailpit
    Wait-ContainerHealthy $mailpitContainer
    Wait-HttpStatus 'http://127.0.0.1:5191/health/ready' 200

    $apiContainerId = docker inspect --format '{{.Id}}' `
        $apiContainer
    $automationContainerId = docker inspect --format '{{.Id}}' `
        $automationContainer
    Invoke-Compose stop postgres
    Assert-LiveReadyFailure `
        'http://127.0.0.1:8080/health/live' `
        'http://127.0.0.1:8080/health/ready' 'postgresApi'
    Assert-LiveReadyFailure `
        'http://127.0.0.1:5191/health/live' `
        'http://127.0.0.1:5191/health/ready' 'postgresAutomation'
    Start-Sleep -Seconds 7
    if ((docker inspect --format '{{.Id}}' $apiContainer) -ne
        $apiContainerId -or
        (docker inspect --format '{{.Id}}' $automationContainer) -ne
        $automationContainerId) {
        throw 'A process container was replaced during PostgreSQL loss.'
    }
    Wait-HttpStatus 'http://127.0.0.1:8080/health/live' 200 10
    Wait-HttpStatus 'http://127.0.0.1:5191/health/live' 200 10
    Invoke-Compose start postgres
    Wait-ContainerHealthy $postgresContainer
    Wait-HttpStatus 'http://127.0.0.1:8080/health/ready' 200 60
    Wait-HttpStatus 'http://127.0.0.1:5191/health/ready' 200 60
    $recoveredSnapshot = Invoke-RestMethod `
        'http://127.0.0.1:5191/internal/operations/status'
    if ($recoveredSnapshot.isStale) {
        throw 'Operator snapshot did not recover after PostgreSQL restart.'
    }
    $results.postgresRecovery = [ordered]@{
        sameApiContainer = $true
        sameAutomationContainer = $true
        snapshotStaleAfterRecovery = $false
    }

    $auth = New-AuthenticatedSession
    Invoke-Compose stop redis
    $accepted = Invoke-RestMethod `
        'http://127.0.0.1:8080/api/v1/walking-skeleton' `
        -Method Post -WebSession $auth.Session -Headers $auth.Headers
    Start-Sleep -Seconds 5
    $pending = Invoke-RestMethod `
        "http://127.0.0.1:8080/api/v1/walking-skeleton/$($accepted.probeId)" `
        -WebSession $auth.Session
    if ($pending.status -eq 'processed') {
        throw 'Redis outage probe unexpectedly completed while Redis was stopped.'
    }
    $attemptSql = 'SELECT "PublishAttempts" FROM outbox_messages WHERE "Id" = ''{0}'';' `
        -f $accepted.messageId
    $attemptOutput = $attemptSql | & docker exec -i `
        $postgresContainer psql -U invoice -d invoice -tA
    if ($LASTEXITCODE -ne 0) { throw 'Could not query outbox attempt evidence.' }
    $attemptsDuringOutage = [int]$attemptOutput
    if ($attemptsDuringOutage -gt 3) {
        throw "Outbox retry amplification detected: $attemptsDuringOutage attempts in five seconds."
    }
    $outageSnapshot = Invoke-RestMethod `
        'http://127.0.0.1:5191/internal/operations/status'
    if ($outageSnapshot.pendingOutboxCount -lt 1) {
        throw 'Redis outage did not become visible as outbox backlog.'
    }
    Invoke-Compose start redis
    Wait-ContainerHealthy $redisContainer
    $deadline = (Get-Date).AddSeconds(60)
    do {
        Start-Sleep -Milliseconds 500
        $processed = Invoke-RestMethod `
            "http://127.0.0.1:8080/api/v1/walking-skeleton/$($accepted.probeId)" `
            -WebSession $auth.Session
    } while ($processed.status -ne 'processed' -and (Get-Date) -lt $deadline)
    if ($processed.status -ne 'processed') {
        throw 'Outbox probe did not recover after Redis restart.'
    }
    $attemptOutput = $attemptSql | & docker exec -i `
        $postgresContainer psql -U invoice -d invoice -tA
    if ($LASTEXITCODE -ne 0) { throw 'Could not query recovered outbox evidence.' }
    $attemptsAfterRecovery = [int]$attemptOutput
    if ($attemptsAfterRecovery -gt 4) {
        throw "Outbox recovery exceeded the bounded expectation: $attemptsAfterRecovery."
    }
    $results.redis = [ordered]@{
        probeId = $accepted.probeId
        messageId = $accepted.messageId
        attemptsDuringFiveSecondOutage = $attemptsDuringOutage
        attemptsAfterRecovery = $attemptsAfterRecovery
        backlogVisible = $true
        finalStatus = $processed.status
    }

    $observerArguments = $composeArguments + @(
        '-f', 'compose.observability.yaml', '--profile', 'observability')
    # The dashboard is explicitly disposable and in-memory. Recreate it so a
    # container retained from an earlier Compose network cannot invalidate the
    # observer-independence probe.
    & docker @observerArguments rm --stop --force aspire-dashboard
    if ($LASTEXITCODE -ne 0) { throw 'Could not reset the disposable observer.' }
    & docker @observerArguments up --detach
    if ($LASTEXITCODE -ne 0) { throw 'Could not start the optional observer profile.' }
    Wait-ContainerHealthy $apiContainer
    Wait-ContainerHealthy $automationContainer
    & docker @observerArguments stop aspire-dashboard
    if ($LASTEXITCODE -ne 0) { throw 'Could not stop the optional observer.' }
    Wait-HttpStatus 'http://127.0.0.1:8080/health/ready' 200 30
    Wait-HttpStatus 'http://127.0.0.1:5191/health/ready' 200 30
    $results.optionalObserver = [ordered]@{
        dashboardStopped = $true
        apiReady = 200
        automationReady = 200
    }

    $results | ConvertTo-Json -Depth 8
}
finally {
    # Restore the normal default topology even when an assertion fails.
    Invoke-Compose up --detach
    Pop-Location
}
