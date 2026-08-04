$ErrorActionPreference = 'Stop'

$spikeRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repositoryRoot = (Resolve-Path (Join-Path $spikeRoot '..\..')).Path
$dotnetRoot = Join-Path $repositoryRoot '.tools\dotnet'
$dotnet = Join-Path $dotnetRoot 'dotnet.exe'
$apiDll = Join-Path $spikeRoot 'AuthContract.Api\bin\Release\net10.0\AuthContract.Api.dll'
$runRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("invoice-phase0-auth-" + [guid]::NewGuid())
$keysPath = Join-Path $runRoot 'keys'
$cookieJar = Join-Path $runRoot 'cookies.txt'
$ownerBody = Join-Path $runRoot 'owner.json'
$viewerJar = Join-Path $runRoot 'viewer-cookies.txt'
$viewerBody = Join-Path $runRoot 'viewer.json'

New-Item -ItemType Directory -Path $keysPath -Force | Out-Null

function Start-Api([int] $port) {
    $environment = @{
        ASPNETCORE_ENVIRONMENT = 'Development'
        ASPNETCORE_URLS = "http://127.0.0.1:$port"
        DataProtection__KeysPath = $keysPath
        DOTNET_ROOT = $dotnetRoot
    }

    foreach ($entry in $environment.GetEnumerator()) {
        [Environment]::SetEnvironmentVariable($entry.Key, $entry.Value, 'Process')
    }

    return Start-Process -FilePath $dotnet -ArgumentList @($apiDll) -PassThru -WindowStyle Hidden `
        -RedirectStandardOutput (Join-Path $runRoot "api-$port.stdout.log") `
        -RedirectStandardError (Join-Path $runRoot "api-$port.stderr.log")
}

function Wait-Ready([int] $port) {
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds(20)
    do {
        try {
            $status = & curl.exe -sS -o NUL -w '%{http_code}' "http://127.0.0.1:$port/health/live"
            if ($status -eq '200') { return }
        }
        catch {
            # The process may still be binding its socket.
        }
        Start-Sleep -Milliseconds 200
    } while ([DateTimeOffset]::UtcNow -lt $deadline)

    throw "API on port $port did not become ready."
}

function Invoke-Curl(
    [string] $method,
    [string] $url,
    [string] $jar,
    [string] $bodyPath,
    [string] $json = '',
    [string] $csrf = ''
) {
    $arguments = @('-sS', '-D', (Join-Path $runRoot 'headers.txt'), '-o', $bodyPath, '-w', '%{http_code}', '-X', $method, '-c', $jar, '-b', $jar)
    if ($json) {
        $jsonPath = Join-Path $runRoot ("request-" + [guid]::NewGuid() + ".json")
        Set-Content -LiteralPath $jsonPath -Value $json -NoNewline -Encoding UTF8
        $arguments += @('-H', 'Content-Type: application/json', '--data-binary', "@$jsonPath")
    }
    if ($csrf) {
        $arguments += @('-H', "X-CSRF-TOKEN: $csrf")
    }
    $arguments += $url
    return & curl.exe @arguments
}

function Get-Csrf([int] $port, [string] $jar, [string] $bodyPath) {
    $status = Invoke-Curl 'GET' "http://127.0.0.1:$port/api/antiforgery" $jar $bodyPath
    if ($status -ne '200') { throw "Expected antiforgery 200, received $status." }
    return (Get-Content -LiteralPath $bodyPath -Raw | ConvertFrom-Json).token
}

function Assert-Status([string] $actual, [string] $expected, [string] $scenario) {
    if ($actual -ne $expected) {
        $body = if (Test-Path -LiteralPath $ownerBody) { Get-Content -LiteralPath $ownerBody -Raw } else { '<none>' }
        $logs = Get-ChildItem -LiteralPath $runRoot -Filter 'api-*.log' |
            ForEach-Object { "[$($_.Name)]`n$(Get-Content -LiteralPath $_.FullName -Raw)" }
        throw "$scenario expected HTTP $expected, received $actual. Body: $body`n$($logs -join "`n")"
    }
}

$processes = @()
try {
    $processes += Start-Api 5181
    Wait-Ready 5181
    # Force creation of the shared key ring before the second replica starts.
    $null = Get-Csrf 5181 $cookieJar $ownerBody
    $processes += Start-Api 5182
    Wait-Ready 5182

    $status = Invoke-Curl 'GET' 'http://127.0.0.1:5181/api/v1/auth/me' $cookieJar $ownerBody
    Assert-Status $status '401' 'Anonymous API request'
    if ((Get-Content -LiteralPath (Join-Path $runRoot 'headers.txt') -Raw) -match '(?im)^Location:') {
        throw 'Anonymous API response redirected instead of returning 401.'
    }

    $csrf = Get-Csrf 5181 $cookieJar $ownerBody
    $status = Invoke-Curl 'POST' 'http://127.0.0.1:5181/api/login' $cookieJar $ownerBody '{"username":"owner","password":"phase0-only"}' $csrf
    Assert-Status $status '200' 'Owner login'

    $status = Invoke-Curl 'GET' 'http://127.0.0.1:5182/api/v1/auth/me' $cookieJar $ownerBody
    Assert-Status $status '200' 'Shared cookie on second replica'

    $status = Invoke-Curl 'POST' 'http://127.0.0.1:5182/api/v1/mutation-proof' $cookieJar $ownerBody '{"value":"missing-token"}'
    Assert-Status $status '400' 'CSRF rejection'

    $csrf = Get-Csrf 5182 $cookieJar $ownerBody
    $status = Invoke-Curl 'POST' 'http://127.0.0.1:5181/api/v1/mutation-proof' $cookieJar $ownerBody '{"value":"shared-antiforgery"}' $csrf
    Assert-Status $status '200' 'Cross-replica antiforgery'

    $viewerCsrf = Get-Csrf 5181 $viewerJar $viewerBody
    $status = Invoke-Curl 'POST' 'http://127.0.0.1:5181/api/login' $viewerJar $viewerBody '{"username":"viewer","password":"phase0-only"}' $viewerCsrf
    Assert-Status $status '200' 'Viewer login'
    $status = Invoke-Curl 'GET' 'http://127.0.0.1:5182/api/v1/owner-proof' $viewerJar $viewerBody
    Assert-Status $status '403' 'Wrong-role API request'

    Write-Output 'PASS anonymous=401 no-redirect'
    Write-Output 'PASS cross-replica-cookie=200'
    Write-Output 'PASS missing-csrf=400'
    Write-Output 'PASS cross-replica-antiforgery=200'
    Write-Output 'PASS wrong-role=403'
}
finally {
    foreach ($process in $processes) {
        if ($process -and -not $process.HasExited) {
            Stop-Process -Id $process.Id
            $process.WaitForExit()
        }
    }
    if (Test-Path -LiteralPath $runRoot) {
        Remove-Item -LiteralPath $runRoot -Recurse -Force
    }
}
