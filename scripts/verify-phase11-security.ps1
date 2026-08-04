[CmdletBinding()]
param(
    [ValidatePattern('^[a-z0-9][a-z0-9_-]+$')]
    [string]$ProjectName = 'business-invoice-tracker'
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$baseUri = 'http://127.0.0.1:8080'
$demoPassword = 'Phase2-local-only!'

function Assert-NativeSuccess([string]$operation) {
    if ($LASTEXITCODE -ne 0) {
        throw "$operation failed with exit code $LASTEXITCODE."
    }
}

function Invoke-AllowHttpError([hashtable]$parameters) {
    try {
        return Invoke-WebRequest @parameters
    }
    catch {
        if ($null -eq $_.Exception.Response) { throw }
        return [pscustomobject]@{
            StatusCode = [int]$_.Exception.Response.StatusCode
            Headers = $_.Exception.Response.Headers
        }
    }
}

function Reset-ApiPair {
    docker compose --project-name $ProjectName --env-file .env.example `
        --profile replica-test stop api-dapr api
    Assert-NativeSuccess 'security API pair stop'
    docker compose --project-name $ProjectName --env-file .env.example `
        --profile replica-test start api
    Assert-NativeSuccess 'security API start'
    docker compose --project-name $ProjectName --env-file .env.example `
        --profile replica-test start api-dapr
    Assert-NativeSuccess 'security API sidecar start'
    $deadline = (Get-Date).AddSeconds(180)
    do {
        $health = docker inspect --format '{{.State.Health.Status}}' `
            "$ProjectName-api-1" 2>$null
        if ($health -eq 'healthy') { return }
        Start-Sleep -Seconds 2
    } while ((Get-Date) -lt $deadline)
    throw 'API did not become healthy after security limiter reset.'
}

Push-Location $repositoryRoot
try {
    & "$PSScriptRoot\verify-phase2.ps1" -ProjectName $ProjectName
    if (-not $?) { throw 'Retained authorization matrix failed.' }

    # Phase 2 deliberately performs several logins. Reset only the process pair
    # so the abuse-limit proof starts from a deterministic window while durable
    # database and shared-key state remain retained.
    Reset-ApiPair

    $root = Invoke-WebRequest -UseBasicParsing $baseUri
    $api = Invoke-WebRequest -UseBasicParsing "$baseUri/health/live"
    foreach ($response in @($root, $api)) {
        if ($response.Headers['Content-Security-Policy'] -notmatch
            "default-src 'self'.*frame-ancestors 'none'") {
            throw 'Same-origin response is missing the restrictive CSP.'
        }
        if ($response.Headers['X-Content-Type-Options'] -notmatch 'nosniff' -or
            $response.Headers['Referrer-Policy'] -notmatch 'no-referrer' -or
            $response.Headers['X-Frame-Options'] -notmatch 'DENY') {
            throw 'Same-origin security headers are incomplete.'
        }
        if ($response.Headers['Server'] -match '/[0-9]') {
            throw "Server header exposes a version: $($response.Headers['Server'])"
        }
    }

    $session = New-Object Microsoft.PowerShell.Commands.WebRequestSession
    $antiforgeryResponse = Invoke-WebRequest -UseBasicParsing `
        "$baseUri/api/v1/auth/antiforgery" -WebSession $session
    $antiforgery = $antiforgeryResponse.Content | ConvertFrom-Json
    $antiforgeryCookies = @($antiforgeryResponse.Headers['Set-Cookie']) -join ';'
    if ($antiforgeryCookies -notmatch 'invoice-antiforgery=.*HttpOnly' -or
        $antiforgeryCookies -notmatch 'SameSite=Strict') {
        throw 'Antiforgery cookie attributes are incomplete.'
    }

    $loginBody = @{
        email = 'owner@acme.test'
        password = $demoPassword
    } | ConvertTo-Json
    $login = Invoke-WebRequest -UseBasicParsing "$baseUri/api/v1/auth/login" `
        -Method Post -WebSession $session `
        -Headers @{ $antiforgery.headerName = $antiforgery.token } `
        -ContentType 'application/json' -Body $loginBody
    $authCookies = @($login.Headers['Set-Cookie']) -join ';'
    if ($authCookies -notmatch 'invoice-auth=.*HttpOnly' -or
        $authCookies -notmatch 'SameSite=Strict') {
        throw 'Authentication cookie attributes are incomplete.'
    }

    $missingCsrf = Invoke-AllowHttpError @{
        Uri = "$baseUri/api/v1/auth/logout"
        Method = 'Post'
        WebSession = $session
        UseBasicParsing = $true
    }
    if ($missingCsrf.StatusCode -ne 400) {
        throw "Authenticated unsafe request without CSRF returned $($missingCsrf.StatusCode)."
    }

    $trace = Invoke-AllowHttpError @{
        Uri = "$baseUri/api/v1/auth/me"
        Method = 'Trace'
        WebSession = $session
        UseBasicParsing = $true
    }
    if ($trace.StatusCode -notin @(400, 405)) {
        throw "Unsupported TRACE request returned $($trace.StatusCode)."
    }

    $limited = $false
    $statuses = [Collections.Generic.List[int]]::new()
    for ($attempt = 1; $attempt -le 12; $attempt++) {
        $probeSession = New-Object Microsoft.PowerShell.Commands.WebRequestSession
        $token = Invoke-RestMethod "$baseUri/api/v1/auth/antiforgery" `
            -WebSession $probeSession
        $badLogin = Invoke-AllowHttpError @{
            Uri = "$baseUri/api/v1/auth/login"
            Method = 'Post'
            WebSession = $probeSession
            Headers = @{ $token.headerName = $token.token }
            ContentType = 'application/json'
            Body = (@{ email='nobody@example.test'; password='Wrong-password-1!' } | ConvertTo-Json)
            UseBasicParsing = $true
        }
        $statuses.Add([int]$badLogin.StatusCode)
        if ($badLogin.StatusCode -eq 429) { $limited = $true }
    }
    if (-not $limited -or $statuses[0] -eq 429) {
        throw "Login limiter did not admit a bounded initial window then reject abuse: $($statuses -join ',')."
    }
    if (($statuses | Where-Object { $_ -notin @(401, 429) }).Count -gt 0) {
        throw "Unexpected login abuse statuses: $($statuses -join ',')."
    }

    Write-Output "Security headers=PASS cookies=PASS csrf=400 trace=$($trace.StatusCode) loginStatuses=$($statuses -join ',')"
    Write-Output 'PASS Phase 11 OWASP-oriented security verification'
}
finally {
    # A fresh limiter window prevents this verification from changing the next
    # reviewer journey. The retained database, volumes and keys are untouched.
    Reset-ApiPair
    Pop-Location
}
