[CmdletBinding()]
param([string]$BaseUri = 'http://127.0.0.1:8080')

$ErrorActionPreference = 'Stop'
$businessId = '11111111-1111-4111-8111-111111111111'
$demoPassword = 'Phase2-local-only!'

function Invoke-AllowHttpError {
    param(
        [string]$Uri,
        [Microsoft.PowerShell.Commands.WebRequestSession]$Session,
        [string]$Method = 'Get',
        [hashtable]$Headers = @{},
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
        if ($Body) {
            $parameters.ContentType = 'application/json'
            $parameters.Body = $Body
        }
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

function New-AuthenticatedSession([string]$email) {
    $session = New-Object Microsoft.PowerShell.Commands.WebRequestSession
    $token = Invoke-RestMethod "$BaseUri/api/v1/auth/antiforgery" -WebSession $session
    $headers = @{ $token.headerName = $token.token }
    $body = @{ email = $email; password = $demoPassword } | ConvertTo-Json
    $response = Invoke-AllowHttpError "$BaseUri/api/v1/auth/login" $session Post $headers $body
    if ($response.StatusCode -ne 200) { throw "Login failed for $email." }
    return $session
}

function Get-AntiforgeryHeaders([Microsoft.PowerShell.Commands.WebRequestSession]$session) {
    $token = Invoke-RestMethod "$BaseUri/api/v1/auth/antiforgery" -WebSession $session
    return @{ $token.headerName = $token.token }
}

function New-CustomerBody([int]$number, [string]$name = '') {
    $suffix = $number.ToString('000000000000')
    if ([string]::IsNullOrEmpty($name)) { $name = "Phase5 Customer $number" }
    return @{
        requestId = "50000000-0000-4000-8000-$suffix"
        name = $name
        email = "phase5-$($number.ToString('00'))@test.local"
        phone = "+27 11 555 $($number.ToString('0000'))"
        billingAddressLine1 = "$number Verification Road"
        billingAddressLine2 = $null
        billingCity = 'Johannesburg'
        billingPostalCode = '2000'
        billingCountryCode = 'ZA'
    }
}

function Assert-Status($response, [int[]]$expected, [string]$operation) {
    if ($expected -notcontains [int]$response.StatusCode) {
        throw "$operation returned $($response.StatusCode); expected $($expected -join ' or '). Body: $($response.Content)"
    }
}

$owner = New-AuthenticatedSession 'owner@acme.test'
$ownerHeaders = Get-AntiforgeryHeaders $owner

# The stale-write scenario deliberately edits fixture 2. Restore that known
# fixture before replaying create checks so this gate remains safe to rerun
# after either a completed or interrupted prior verification.
$fixtureId = '50000000-0000-4000-8000-000000000002'
$existingFixture = Invoke-AllowHttpError `
    "$BaseUri/api/v1/businesses/$businessId/customers/$fixtureId" `
    $owner
if ($existingFixture.StatusCode -eq 200) {
    $currentFixture = $existingFixture.Content | ConvertFrom-Json
    $baselineFixture = New-CustomerBody 2
    $restoreBody = @{
        version = $currentFixture.version
        name = $baselineFixture.name
        email = $baselineFixture.email
        phone = $baselineFixture.phone
        billingAddressLine1 = $baselineFixture.billingAddressLine1
        billingAddressLine2 = $baselineFixture.billingAddressLine2
        billingCity = $baselineFixture.billingCity
        billingPostalCode = $baselineFixture.billingPostalCode
        billingCountryCode = $baselineFixture.billingCountryCode
    } | ConvertTo-Json
    $restored = Invoke-AllowHttpError `
        "$BaseUri/api/v1/businesses/$businessId/customers/$fixtureId" `
        $owner Put $ownerHeaders $restoreBody
    Assert-Status $restored @(200) 'restore customer update fixture'
}
elseif ($existingFixture.StatusCode -ne 404) {
    throw "Update fixture lookup returned $($existingFixture.StatusCode)."
}

for ($number = 1; $number -le 23; $number++) {
    $body = New-CustomerBody $number | ConvertTo-Json
    $response = Invoke-AllowHttpError `
        "$BaseUri/api/v1/businesses/$businessId/customers" `
        $owner Post $ownerHeaders $body
    Assert-Status $response @(200, 201) "create customer $number"
}

$firstPage = Invoke-RestMethod `
    "$BaseUri/api/v1/businesses/$businessId/customers?page=1&pageSize=10" `
    -WebSession $owner
$secondPage = Invoke-RestMethod `
    "$BaseUri/api/v1/businesses/$businessId/customers?page=2&pageSize=10" `
    -WebSession $owner
if ($firstPage.items.Count -ne 10 -or $secondPage.items.Count -ne 10) {
    throw 'Server pagination did not return the requested page size.'
}
$overlap = @($firstPage.items.id | Where-Object { $secondPage.items.id -contains $_ })
if ($overlap.Count -ne 0 -or $firstPage.totalCount -lt 23 -or $firstPage.totalPages -lt 3) {
    throw 'Pagination metadata or unique ordering is incorrect.'
}

$encodedSearch = [Uri]::EscapeDataString('phase5-17@test.local')
$searchResult = Invoke-RestMethod `
    "$BaseUri/api/v1/businesses/$businessId/customers?search=$encodedSearch&page=1&pageSize=10" `
    -WebSession $owner
if ($searchResult.totalCount -ne 1 -or $searchResult.items[0].email -ne 'phase5-17@test.local') {
    throw 'Server search did not return the unique matching customer.'
}

$repeatBody = New-CustomerBody 1 | ConvertTo-Json
$repeat = Invoke-AllowHttpError `
    "$BaseUri/api/v1/businesses/$businessId/customers" `
    $owner Post $ownerHeaders $repeatBody
Assert-Status $repeat @(200) 'identical duplicate create'
$reusedBody = New-CustomerBody 1 'Different payload' | ConvertTo-Json
$reused = Invoke-AllowHttpError `
    "$BaseUri/api/v1/businesses/$businessId/customers" `
    $owner Post $ownerHeaders $reusedBody
Assert-Status $reused @(409) 'conflicting duplicate create'

$viewer = New-AuthenticatedSession 'viewer@acme.test'
$viewerHeaders = Get-AntiforgeryHeaders $viewer
$viewerBody = New-CustomerBody 99 | ConvertTo-Json
$viewerCreate = Invoke-AllowHttpError `
    "$BaseUri/api/v1/businesses/$businessId/customers" `
    $viewer Post $viewerHeaders $viewerBody
Assert-Status $viewerCreate @(403) 'viewer customer create'

$otherOwner = New-AuthenticatedSession 'owner@other.test'
$crossTenant = Invoke-AllowHttpError `
    "$BaseUri/api/v1/businesses/$businessId/customers/50000000-0000-4000-8000-000000000001" `
    $otherOwner
Assert-Status $crossTenant @(404) 'cross-tenant customer read'

$current = Invoke-RestMethod `
    "$BaseUri/api/v1/businesses/$businessId/customers/50000000-0000-4000-8000-000000000002" `
    -WebSession $owner
$update = @{
    version = $current.version
    name = 'Phase5 Updated Customer'
    email = 'phase5-02@test.local'
    phone = $null
    billingAddressLine1 = '2 Updated Road'
    billingAddressLine2 = 'Suite 5'
    billingCity = 'Cape Town'
    billingPostalCode = '8001'
    billingCountryCode = 'ZA'
} | ConvertTo-Json
$updated = Invoke-AllowHttpError `
    "$BaseUri/api/v1/businesses/$businessId/customers/$($current.id)" `
    $owner Put $ownerHeaders $update
Assert-Status $updated @(200) 'customer update'
$stale = Invoke-AllowHttpError `
    "$BaseUri/api/v1/businesses/$businessId/customers/$($current.id)" `
    $owner Put $ownerHeaders $update
Assert-Status $stale @(409) 'stale customer update'

$invalidBody = New-CustomerBody 98
$invalidBody.name = ''
$invalid = Invoke-AllowHttpError `
    "$BaseUri/api/v1/businesses/$businessId/customers" `
    $owner Post $ownerHeaders ($invalidBody | ConvertTo-Json)
Assert-Status $invalid @(400) 'invalid customer create'

[pscustomobject]@{
    phase = 5
    gateChecks = 'api-pagination-search-authorization-concurrency-idempotency-validation'
    totalCustomers = $firstPage.totalCount
    pageSize = $firstPage.pageSize
    totalPages = $firstPage.totalPages
    crossTenantStatus = $crossTenant.StatusCode
    staleUpdateStatus = $stale.StatusCode
} | ConvertTo-Json
