[CmdletBinding()]
param(
    [switch] $Observability,
    [switch] $KeepRunning
)

$ErrorActionPreference = 'Stop'
$composeRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repositoryRoot = (Resolve-Path (Join-Path $composeRoot '..\..')).Path
$artifactRoot = Join-Path $repositoryRoot 'artifacts\phase0-compose'
$composeFiles = @('-f', (Join-Path $composeRoot 'compose.yaml'))
if ($Observability) {
    $composeFiles += @(
        '-f', (Join-Path $composeRoot 'compose.observability.yaml'),
        '--profile', 'observability'
    )
}

New-Item -ItemType Directory -Path $artifactRoot -Force | Out-Null
$authArtifactPaths = @(
    (Join-Path $artifactRoot 'auth-cookies.txt'),
    (Join-Path $artifactRoot 'auth-body.json'),
    (Join-Path $artifactRoot 'auth-headers.txt')
)
Remove-Item -LiteralPath $authArtifactPaths -Force -ErrorAction SilentlyContinue

function Invoke-Compose {
    & docker compose @composeFiles @args
    if ($LASTEXITCODE -ne 0) {
        throw "docker compose $($args -join ' ') exited with $LASTEXITCODE."
    }
}

function Wait-Http([string] $url, [int] $seconds = 90) {
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($seconds)
    do {
        try {
            $status = & curl.exe -sS -o NUL -w '%{http_code}' $url 2>$null
            if ($status -in @('200', '204')) {
                return
            }
        }
        catch {
            # Connection refusal/reset is expected while a restarted process
            # binds its socket. The bounded deadline still fails closed.
        }
        Start-Sleep -Milliseconds 500
    } while ([DateTimeOffset]::UtcNow -lt $deadline)
    throw "$url did not return HTTP 200 within $seconds seconds."
}

function Invoke-Json(
    [string] $method,
    [string] $url,
    [string] $json
) {
    $requestPath = Join-Path $artifactRoot ("request-" + [guid]::NewGuid() + '.json')
    Set-Content -LiteralPath $requestPath -Value $json -NoNewline -Encoding UTF8
    $response = & curl.exe -sS -f -X $method -H 'Content-Type: application/json' `
        --data-binary "@$requestPath" $url
    if ($LASTEXITCODE -ne 0) {
        throw "$method $url failed."
    }
    return $response | ConvertFrom-Json
}

function Invoke-AuthCurl(
    [string] $method,
    [string] $path,
    [string] $cookieJar,
    [string] $bodyPath,
    [string] $json = '',
    [string] $csrf = ''
) {
    $headerPath = Join-Path $artifactRoot 'auth-headers.txt'
    $arguments = @(
        '-sS', '-D', $headerPath, '-o', $bodyPath, '-w', '%{http_code}',
        '-X', $method, '-c', $cookieJar, '-b', $cookieJar
    )
    if ($json) {
        $requestPath = Join-Path $artifactRoot ("auth-request-" + [guid]::NewGuid() + '.json')
        Set-Content -LiteralPath $requestPath -Value $json -NoNewline -Encoding UTF8
        $arguments += @('-H', 'Content-Type: application/json', '--data-binary', "@$requestPath")
    }
    if ($csrf) {
        $arguments += @('-H', "X-CSRF-TOKEN: $csrf")
    }
    $arguments += "http://127.0.0.1:8080$path"
    return & curl.exe @arguments
}

function Wait-Workflow(
    [string] $instanceId,
    [string] $completionKind,
    [int] $seconds = 90
) {
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($seconds)
    do {
        try {
            $state = Invoke-RestMethod -Uri "http://127.0.0.1:5190/workflows/$instanceId"
            if ($state.isWorkflowCompleted) {
                if ($state.output.completionKind -ne $completionKind) {
                    throw "Workflow $instanceId completed as $($state.output.completionKind), expected $completionKind."
                }
                return
            }
        }
        catch {
            if ([DateTimeOffset]::UtcNow -ge $deadline) { throw }
        }
        Start-Sleep -Milliseconds 500
    } while ([DateTimeOffset]::UtcNow -lt $deadline)
    throw "Workflow $instanceId did not complete as $completionKind."
}

function Wait-Telemetry([int] $seconds = 90) {
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($seconds)
    do {
        try {
            $resources = Invoke-RestMethod `
                -Uri 'http://127.0.0.1:18888/api/telemetry/resources'
            $expected = @('phase0-auth-api', 'phase0-workflow-worker')
            $complete = $true
            foreach ($serviceName in $expected) {
                $resource = $resources |
                    Where-Object {
                        $_.name -eq $serviceName -and
                        $_.hasLogs -and $_.hasTraces -and $_.hasMetrics
                    } |
                    Select-Object -First 1
                if (-not $resource) {
                    $complete = $false
                    break
                }
            }
            if ($complete) {
                return
            }
        }
        catch {
            # Export is asynchronous and the dashboard API may still be binding.
        }
        Start-Sleep -Seconds 1
    } while ([DateTimeOffset]::UtcNow -lt $deadline)
    throw 'Aspire dashboard did not receive logs, traces and metrics from both .NET services.'
}

try {
    & docker compose version
    if ($LASTEXITCODE -ne 0) {
        throw 'Docker Compose v2 is required.'
    }

    Invoke-Compose up --build --detach
    Wait-Http 'http://127.0.0.1:8080/'
    Wait-Http 'http://127.0.0.1:8080/health/live'
    Wait-Http 'http://127.0.0.1:5190/health/live'
    # Dapr documents this as an infrastructure readiness endpoint. It returns
    # 204 only after components and the application channel are initialized.
    Wait-Http 'http://127.0.0.1:3520/v1.0/healthz'

    $authJar = Join-Path $artifactRoot 'auth-cookies.txt'
    $authBody = Join-Path $artifactRoot 'auth-body.json'
    $status = Invoke-AuthCurl GET '/api/v1/auth/me' $authJar $authBody
    if ($status -ne '401') {
        throw "Anonymous same-origin request returned $status instead of 401."
    }
    if ((Get-Content -LiteralPath (Join-Path $artifactRoot 'auth-headers.txt') -Raw) -match '(?im)^Location:') {
        throw 'Anonymous same-origin API request redirected.'
    }
    $status = Invoke-AuthCurl GET '/api/antiforgery' $authJar $authBody
    if ($status -ne '200') {
        throw "Antiforgery endpoint returned $status."
    }
    $csrf = (Get-Content -LiteralPath $authBody -Raw | ConvertFrom-Json).token
    $status = Invoke-AuthCurl POST '/api/login' $authJar $authBody `
        '{"username":"owner","password":"phase0-only"}' $csrf
    if ($status -ne '200') {
        throw "Same-origin login returned $status."
    }
    $status = Invoke-AuthCurl POST '/api/v1/mutation-proof' $authJar $authBody `
        '{"value":"missing-token"}'
    if ($status -ne '400') {
        throw "Same-origin missing-CSRF mutation returned $status instead of 400."
    }
    Write-Output 'PASS nginx-cookie-antiforgery-same-origin'

    # Event is raised while the workflow is still in its pre-registration timer.
    $eventId = 'buffered-' + [guid]::NewGuid().ToString('N')
    $null = Invoke-Json POST "http://127.0.0.1:5190/workflows/$eventId" `
        '{"dueInSeconds":30,"waitBeforeEventRegistrationSeconds":5}'
    $null = Invoke-Json POST "http://127.0.0.1:5190/workflows/$eventId/events/control" `
        '{"action":"paid"}'
    Wait-Workflow $eventId 'external-event'
    Write-Output 'PASS dapr-external-event-buffered-before-wait'

    # Restart both the worker and its sidecar while a durable timer is pending.
    $timerId = 'restart-' + [guid]::NewGuid().ToString('N')
    $null = Invoke-Json POST "http://127.0.0.1:5190/workflows/$timerId" `
        '{"dueInSeconds":12,"waitBeforeEventRegistrationSeconds":0}'
    Start-Sleep -Seconds 2
    Invoke-Compose stop workflow-dapr workflow-api
    Invoke-Compose start workflow-api
    Wait-Http 'http://127.0.0.1:5190/health/live'
    Invoke-Compose start workflow-dapr
    Wait-Http 'http://127.0.0.1:3520/v1.0/healthz'
    Wait-Workflow $timerId 'timer'
    Write-Output 'PASS dapr-durable-timer-after-worker-and-sidecar-restart'

    if ($Observability) {
        Wait-Http 'http://127.0.0.1:18888/structuredlogs'
        Wait-Telemetry
        Write-Output 'PASS optional-observability-profile-received-logs-traces-metrics'
    }

    Write-Output 'PASS nginx-same-origin-health'
    Write-Output 'PASS compose-topology'
}
finally {
    try {
        & docker compose @composeFiles logs --no-color *>&1 |
            Set-Content -LiteralPath (Join-Path $artifactRoot 'compose.log') -Encoding UTF8
    }
    catch {
        Write-Warning "Could not capture Compose logs: $($_.Exception.Message)"
    }
    if (-not $KeepRunning) {
        & docker compose @composeFiles down --volumes --remove-orphans
    }
}
