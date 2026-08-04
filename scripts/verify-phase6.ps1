[CmdletBinding()]
param([string]$BaseUri = 'http://127.0.0.1:8080')

$ErrorActionPreference = 'Stop'
$businessId = '11111111-1111-4111-8111-111111111111'
$invoiceId = '60000000-0000-4000-8000-000000000001'
$password = 'Phase2-local-only!'

function Invoke-AllowHttpError {
    param([string]$Uri, [Microsoft.PowerShell.Commands.WebRequestSession]$Session,
        [string]$Method = 'Get', [hashtable]$Headers = @{}, [string]$Body)
    try {
        $p = @{ Uri=$Uri; Method=$Method; WebSession=$Session; Headers=$Headers; UseBasicParsing=$true }
        if ($Body) { $p.ContentType='application/json'; $p.Body=$Body }
        return Invoke-WebRequest @p
    }
    catch {
        $raw = $_.Exception.Response
        if ($null -eq $raw) { throw }
        $reader = New-Object IO.StreamReader($raw.GetResponseStream())
        try { $content = $reader.ReadToEnd() } finally { $reader.Dispose() }
        return [pscustomobject]@{ StatusCode=[int]$raw.StatusCode; Headers=$raw.Headers; Content=$content }
    }
}

function New-Session([string]$email) {
    $session = New-Object Microsoft.PowerShell.Commands.WebRequestSession
    $token = Invoke-RestMethod "$BaseUri/api/v1/auth/antiforgery" -WebSession $session
    $headers = @{}; $headers[$token.headerName] = $token.token
    $body = @{email=$email;password=$password} | ConvertTo-Json
    $response = Invoke-AllowHttpError "$BaseUri/api/v1/auth/login" $session Post $headers $body
    if ($response.StatusCode -ne 200) { throw "Login failed for $email." }
    return $session
}

function Get-Headers($session) {
    $token = Invoke-RestMethod "$BaseUri/api/v1/auth/antiforgery" -WebSession $session
    $headers = @{}; $headers[$token.headerName] = $token.token
    return $headers
}

function Assert-Status($response, [int[]]$expected, [string]$operation) {
    if ($expected -notcontains [int]$response.StatusCode) {
        throw "$operation returned $($response.StatusCode); expected $($expected -join ' or '). Body: $($response.Content)"
    }
}

function New-Draft([string]$description = 'Consulting service') {
    return @{
        requestId = $invoiceId
        customerId = '50000000-0000-4000-8000-000000000001'
        currency = 'ZAR'
        issueDate = '2026-07-31'
        dueDate = '2026-08-31'
        lines = @(
            @{description=$description;quantity=2;unitPrice=100;taxRate=15},
            @{description='Hosting';quantity=1.5;unitPrice=20;taxRate=0}
        )
    }
}

$owner = New-Session 'owner@acme.test'
$headers = Get-Headers $owner
$existing = Invoke-AllowHttpError "$BaseUri/api/v1/businesses/$businessId/invoices/$invoiceId" $owner
if ($existing.StatusCode -eq 200) {
    $current = $existing.Content | ConvertFrom-Json
    $baseline = New-Draft
    $reset = @{
        version=$current.version; customerId=$baseline.customerId; currency=$baseline.currency
        issueDate=$baseline.issueDate; dueDate=$baseline.dueDate; lines=$baseline.lines
    } | ConvertTo-Json -Depth 8
    $restored = Invoke-AllowHttpError "$BaseUri/api/v1/businesses/$businessId/invoices/$invoiceId" $owner Put $headers $reset
    Assert-Status $restored @(200) 'restore draft fixture'
}
elseif ($existing.StatusCode -ne 404) { throw "Draft fixture lookup returned $($existing.StatusCode)." }

$body = New-Draft | ConvertTo-Json -Depth 8
$created = Invoke-AllowHttpError "$BaseUri/api/v1/businesses/$businessId/invoices" $owner Post $headers $body
Assert-Status $created @(200,201) 'create draft'
$draft = $created.Content | ConvertFrom-Json
if ($draft.status -ne 'Draft' -or $null -ne $draft.number -or
    [decimal]$draft.subtotalAmount -ne 230 -or [decimal]$draft.taxAmount -ne 30 -or
    [decimal]$draft.totalAmount -ne 260 -or [decimal]$draft.outstandingAmount -ne 260) {
    throw 'Server-calculated draft totals or lifecycle fields are incorrect.'
}

$replay = Invoke-AllowHttpError "$BaseUri/api/v1/businesses/$businessId/invoices" $owner Post $headers $body
Assert-Status $replay @(200) 'identical draft replay'
$conflictBody = New-Draft 'Different service' | ConvertTo-Json -Depth 8
$conflict = Invoke-AllowHttpError "$BaseUri/api/v1/businesses/$businessId/invoices" $owner Post $headers $conflictBody
Assert-Status $conflict @(409) 'conflicting draft replay'

$viewer = New-Session 'viewer@acme.test'
$viewerHeaders = Get-Headers $viewer
$viewerCreate = Invoke-AllowHttpError "$BaseUri/api/v1/businesses/$businessId/invoices" $viewer Post $viewerHeaders $body
Assert-Status $viewerCreate @(403) 'viewer draft create'

$other = New-Session 'owner@other.test'
$crossTenant = Invoke-AllowHttpError "$BaseUri/api/v1/businesses/$businessId/invoices/$invoiceId" $other
Assert-Status $crossTenant @(404) 'cross-tenant draft read'

$current = Invoke-RestMethod "$BaseUri/api/v1/businesses/$businessId/invoices/$invoiceId" -WebSession $owner
$update = @{
    version=$current.version; customerId=$current.customerId; currency='ZAR'
    issueDate='2026-07-31'; dueDate='2026-09-15'
    lines=@(@{description='Updated consulting';quantity=3;unitPrice=100;taxRate=15})
} | ConvertTo-Json -Depth 8
$updated = Invoke-AllowHttpError "$BaseUri/api/v1/businesses/$businessId/invoices/$invoiceId" $owner Put $headers $update
Assert-Status $updated @(200) 'update draft'
$updatedDraft = $updated.Content | ConvertFrom-Json
if ([decimal]$updatedDraft.totalAmount -ne 345) { throw 'Updated server total is incorrect.' }
$stale = Invoke-AllowHttpError "$BaseUri/api/v1/businesses/$businessId/invoices/$invoiceId" $owner Put $headers $update
Assert-Status $stale @(409) 'stale draft update'

$audit = Invoke-RestMethod "$BaseUri/api/v1/businesses/$businessId/invoices/$invoiceId/audit?page=1&pageSize=20" -WebSession $owner
if ($audit.items.Count -lt 2 -or 'DraftCreated' -notin $audit.items.action -or 'DraftUpdated' -notin $audit.items.action) {
    throw 'Create/update audit history is incomplete.'
}
$list = Invoke-RestMethod "$BaseUri/api/v1/businesses/$businessId/invoices?page=1&pageSize=10" -WebSession $owner
if ($list.items.id -notcontains $invoiceId -or $list.pageSize -ne 10) {
    throw 'Draft list did not return the persisted invoice through server pagination.'
}

[pscustomobject]@{
    phase=6; gateChecks='totals-replay-authorization-concurrency-audit'
    total=$draft.totalAmount; updatedTotal=$updatedDraft.totalAmount
    crossTenantStatus=$crossTenant.StatusCode; staleStatus=$stale.StatusCode
    auditEntries=$audit.items.Count
} | ConvertTo-Json
