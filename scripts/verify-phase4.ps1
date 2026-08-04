[CmdletBinding()]
param(
    [string]$BaseUri = 'http://127.0.0.1:8080'
)

$ErrorActionPreference = 'Stop'

function Invoke-AllowHttpError {
    param(
        [string]$Uri,
        [Microsoft.PowerShell.Commands.WebRequestSession]$Session,
        [string]$Method = 'Get',
        [hashtable]$Headers = @{},
        [string]$ContentType,
        [string]$Body
    )
    try {
        $parameters = @{
            Uri = $Uri
            Method = $Method
            WebSession = $Session
            Headers = $Headers
            UseBasicParsing = $true
        }
        if ($ContentType) { $parameters.ContentType = $ContentType }
        if ($Body) { $parameters.Body = $Body }
        return Invoke-WebRequest @parameters
    }
    catch {
        $raw = $_.Exception.Response
        if ($null -eq $raw) { throw }
        $reader = New-Object IO.StreamReader($raw.GetResponseStream())
        try { $content = $reader.ReadToEnd() }
        finally { $reader.Dispose() }
        return [pscustomobject]@{
            StatusCode = [int]$raw.StatusCode
            Headers = $raw.Headers
            Content = $content
        }
    }
}

function Assert-ProblemResponse($response, [int]$status) {
    if ($response.StatusCode -ne $status) {
        throw "Expected $status but received $($response.StatusCode)."
    }
    if ($response.Headers['Content-Type'] -notmatch '^application/problem\+json') {
        throw "Expected application/problem+json for $status."
    }
    $problem = $response.Content | ConvertFrom-Json
    if ($problem.status -ne $status -or [string]::IsNullOrWhiteSpace($problem.title) -or
        [string]::IsNullOrWhiteSpace($problem.type)) {
        throw "Problem Details for $status is incomplete."
    }
}

$anonymous = New-Object Microsoft.PowerShell.Commands.WebRequestSession
$unauthorized = Invoke-AllowHttpError "$BaseUri/api/v1/auth/me" $anonymous
Assert-ProblemResponse $unauthorized 401

$token = Invoke-RestMethod "$BaseUri/api/v1/auth/antiforgery" -WebSession $anonymous
$invalidBody = @{ email = 'invalid'; password = '' } | ConvertTo-Json
$invalid = Invoke-AllowHttpError "$BaseUri/api/v1/auth/login" $anonymous Post `
    @{ $token.headerName = $token.token } 'application/json' $invalidBody
Assert-ProblemResponse $invalid 400
$validation = $invalid.Content | ConvertFrom-Json
if (-not $validation.errors.email -or -not $validation.errors.password) {
    throw 'Validation Problem Details did not contain field errors.'
}

$loginBody = @{ email = 'viewer@acme.test'; password = 'Phase2-local-only!' } | ConvertTo-Json
$login = Invoke-AllowHttpError "$BaseUri/api/v1/auth/login" $anonymous Post `
    @{ $token.headerName = $token.token } 'application/json' $loginBody
if ($login.StatusCode -ne 200) { throw 'Viewer login failed.' }

$missing = Invoke-AllowHttpError `
    "$BaseUri/api/v1/businesses/aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa" $anonymous
Assert-ProblemResponse $missing 404

$forbidden = Invoke-AllowHttpError `
    "$BaseUri/api/v1/businesses/11111111-1111-4111-8111-111111111111/memberships" $anonymous
Assert-ProblemResponse $forbidden 403

Write-Host 'Phase 4 API Problem Details checks passed for validation, 401, 403, and 404.'
