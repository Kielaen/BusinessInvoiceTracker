[CmdletBinding()]
param(
    [string]$BaseUri = 'http://127.0.0.1:8080',
    [string]$DatabaseContainer = 'business-invoice-tracker-postgres-1'
)

$ErrorActionPreference = 'Stop'
$businessId = '11111111-1111-4111-8111-111111111111'
$otherBusinessId = '22222222-2222-4222-8222-222222222222'
$password = 'Phase2-local-only!'
$marker = "Phase 9 $([guid]::NewGuid().ToString('N'))"

function Invoke-AllowHttpError {
    param([string]$Uri, [Microsoft.PowerShell.Commands.WebRequestSession]$Session)
    try {
        return Invoke-WebRequest $Uri -WebSession $Session -UseBasicParsing
    }
    catch {
        $raw = $_.Exception.Response
        if ($null -eq $raw) { throw }
        $reader = New-Object IO.StreamReader($raw.GetResponseStream())
        try { $content = $reader.ReadToEnd() } finally { $reader.Dispose() }
        return [pscustomobject]@{ StatusCode=[int]$raw.StatusCode; Content=$content }
    }
}

function New-Session([string]$email) {
    $session = New-Object Microsoft.PowerShell.Commands.WebRequestSession
    $token = Invoke-RestMethod "$BaseUri/api/v1/auth/antiforgery" -WebSession $session
    $headers = @{}; $headers[$token.headerName] = $token.token
    $null = Invoke-RestMethod "$BaseUri/api/v1/auth/login" -Method Post `
        -WebSession $session -Headers $headers -ContentType 'application/json' `
        -Body (@{email=$email;password=$password} | ConvertTo-Json)
    $token = Invoke-RestMethod "$BaseUri/api/v1/auth/antiforgery" -WebSession $session
    $headers = @{}; $headers[$token.headerName] = $token.token
    return [pscustomobject]@{ Session=$session; Headers=$headers }
}

function New-Draft(
    [pscustomobject]$auth,
    [guid]$customerId,
    [string]$currency,
    [decimal]$unitPrice,
    [int]$dueOffset,
    [string]$description) {
    $id = [guid]::NewGuid()
    $today = (Get-Date).ToUniversalTime().Date
    $body = @{
        requestId=$id;customerId=$customerId;currency=$currency
        issueDate=$today.AddDays(-2).ToString('yyyy-MM-dd')
        dueDate=$today.AddDays($dueOffset).ToString('yyyy-MM-dd')
        lines=@(@{description=$description;quantity=1;unitPrice=$unitPrice;taxRate=15})
    } | ConvertTo-Json -Depth 8
    try {
        $draft = Invoke-RestMethod "$BaseUri/api/v1/businesses/$businessId/invoices" `
            -Method Post -WebSession $auth.Session -Headers $auth.Headers `
            -ContentType 'application/json' -Body $body
    }
    catch {
        $raw = $_.Exception.Response
        if ($null -eq $raw) { throw }
        $reader = New-Object IO.StreamReader($raw.GetResponseStream())
        try { $content = $reader.ReadToEnd() } finally { $reader.Dispose() }
        throw "Draft '$description' failed with $([int]$raw.StatusCode): $content"
    }
    return [pscustomobject]@{ Id=$id; Invoice=$draft }
}

function Issue-Draft([pscustomobject]$auth, [pscustomobject]$draft) {
    return Invoke-RestMethod `
        "$BaseUri/api/v1/businesses/$businessId/invoices/$($draft.Id)/issue" `
        -Method Post -WebSession $auth.Session -Headers $auth.Headers `
        -ContentType 'application/json' `
        -Body (@{requestId=[guid]::NewGuid();version=$draft.Invoice.version} | ConvertTo-Json)
}

function Currency-Amount($dashboard, [string]$currency, [string]$property) {
    $row = @($dashboard.currencyTotals | Where-Object currency -eq $currency) | Select-Object -First 1
    if ($null -eq $row) { return [decimal]0 }
    return [decimal]$row.$property
}

$owner = New-Session 'owner@acme.test'
$otherOwner = New-Session 'owner@other.test'
$baseline = Invoke-RestMethod "$BaseUri/api/v1/businesses/$businessId/dashboard" `
    -WebSession $owner.Session

$customerId = [guid]::NewGuid()
$customerBody = @{
    requestId=$customerId;name=$marker;email="$($customerId.ToString('N'))@phase9.test"
    phone=$null;billingAddressLine1='9 Query Lane';billingAddressLine2=$null
    billingCity='Johannesburg';billingPostalCode='2000';billingCountryCode='ZA'
} | ConvertTo-Json
$null = Invoke-RestMethod "$BaseUri/api/v1/businesses/$businessId/customers" `
    -Method Post -WebSession $owner.Session -Headers $owner.Headers `
    -ContentType 'application/json' -Body $customerBody

$draft = New-Draft $owner $customerId 'ZAR' 50 5 "$marker draft"
$partialDraft = New-Draft $owner $customerId 'ZAR' 200 5 "$marker partial"
$partial = Issue-Draft $owner $partialDraft
$partial = Invoke-RestMethod `
    "$BaseUri/api/v1/businesses/$businessId/invoices/$($partialDraft.Id)/payments" `
    -Method Post -WebSession $owner.Session -Headers $owner.Headers `
    -ContentType 'application/json' -Body (@{
        requestId=[guid]::NewGuid();version=$partial.version
        idempotencyKey="phase9-$([guid]::NewGuid())";amount=50
        receivedAtUtc=(Get-Date).ToUniversalTime().ToString('o')
    } | ConvertTo-Json)
$overdueDraft = New-Draft $owner $customerId 'ZAR' 100 -1 "$marker overdue"
$overdueIssued = Issue-Draft $owner $overdueDraft
$usdInvoiceId = [guid]::NewGuid()
$usdVersion = [guid]::NewGuid()
$usdNumber = "P9-USD-$($usdInvoiceId.ToString('N').Substring(0,8))"
# The product write boundary intentionally supports ZAR only. This narrow SQL
# fixture proves the read model does not silently combine currencies if historical
# or future imported data contains another valid ISO-style currency code.
$usdSql = @"
INSERT INTO invoices (
  "Id", "BusinessId", "CustomerId", "Number", "NumberSequence",
  "Currency", "Status", "IssueDate", "DueDate", "DueAtUtc",
  "SubtotalAmount", "TaxAmount", "TotalAmount", "PaidAmount", "Version",
  "CreatedAtUtc", "UpdatedAtUtc", "IssuedAtUtc")
VALUES (
  '$usdInvoiceId', '$businessId', '$customerId', '$usdNumber',
  (SELECT COALESCE(MAX("NumberSequence"), 0) + 1 FROM invoices WHERE "BusinessId" = '$businessId'),
  'USD', 'Issued', current_date - 2, current_date + 5,
  (current_date + 6)::timestamptz, 50, 7.50, 57.50, 0, '$usdVersion',
  now(), now(), now());
"@
$null = $usdSql | docker exec -i $DatabaseContainer `
    psql -U invoice -d invoice -v ON_ERROR_STOP=1
if ($LASTEXITCODE -ne 0) { throw 'Could not insert the cross-currency read-model fixture.' }

$deadline = (Get-Date).AddSeconds(75)
do {
    Start-Sleep -Milliseconds 500
    $overdue = Invoke-RestMethod `
        "$BaseUri/api/v1/businesses/$businessId/invoices/$($overdueDraft.Id)" `
        -WebSession $owner.Session
} until ($overdue.status -eq 'Overdue' -or (Get-Date) -ge $deadline)
if ($overdue.status -ne 'Overdue') { throw 'Phase 9 overdue fixture did not reach Overdue.' }

$encodedMarker = [Uri]::EscapeDataString($marker)
$list = Invoke-RestMethod "$BaseUri/api/v1/businesses/$businessId/invoices?search=$encodedMarker&page=1&pageSize=10&sort=amount&direction=desc" `
    -WebSession $owner.Session
if ($list.totalCount -ne 4 -or $list.items.Count -ne 4) {
    throw "Bounded search did not return exactly four Phase 9 fixtures: $($list | ConvertTo-Json -Compress)"
}
$amounts = @($list.items | ForEach-Object { [decimal]$_.totalAmount })
for ($index = 1; $index -lt $amounts.Count; $index++) {
    if ($amounts[$index] -gt $amounts[$index - 1]) { throw 'Amount descending order is unstable.' }
}
$overdueList = Invoke-RestMethod "$BaseUri/api/v1/businesses/$businessId/invoices?search=$encodedMarker&status=Overdue&page=1&pageSize=10&sort=due&direction=asc" `
    -WebSession $owner.Session
if ($overdueList.totalCount -ne 1 -or $overdueList.items[0].id -ne $overdueDraft.Id) {
    throw 'Status/search filtering did not isolate the overdue fixture.'
}
$page = Invoke-RestMethod "$BaseUri/api/v1/businesses/$businessId/invoices?search=$encodedMarker&page=2&pageSize=1&sort=updated&direction=asc" `
    -WebSession $owner.Session
if ($page.items.Count -ne 1 -or $page.totalPages -ne 4) { throw 'Invoice pagination bounds are incorrect.' }

foreach ($query in @('pageSize=101', 'page=10001', 'status=Unknown', 'sort=rawSql', 'direction=sideways')) {
    $response = Invoke-AllowHttpError `
        "$BaseUri/api/v1/businesses/$businessId/invoices?$query" $owner.Session
    if ($response.StatusCode -ne 400) { throw "Invalid query '$query' returned $($response.StatusCode), expected 400." }
}

$dashboard = Invoke-RestMethod "$BaseUri/api/v1/businesses/$businessId/dashboard" `
    -WebSession $owner.Session
if ($dashboard.counts.total -lt ($baseline.counts.total + 4) -or
    $dashboard.counts.draft -lt ($baseline.counts.draft + 1) -or
    $dashboard.counts.open -lt ($baseline.counts.open + 3) -or
    $dashboard.counts.overdue -lt ($baseline.counts.overdue + 1)) {
    throw "Dashboard count deltas are incomplete: $($dashboard.counts | ConvertTo-Json -Compress)"
}
$zarDelta = (Currency-Amount $dashboard 'ZAR' 'outstandingAmount') - `
    (Currency-Amount $baseline 'ZAR' 'outstandingAmount')
$usdDelta = (Currency-Amount $dashboard 'USD' 'outstandingAmount') - `
    (Currency-Amount $baseline 'USD' 'outstandingAmount')
if ($zarDelta -ne 295 -or $usdDelta -ne 57.5) {
    throw "Currency-aware totals were not separated: ZAR delta=$zarDelta USD delta=$usdDelta"
}
if ($dashboard.overdueInvoices.Count -gt 5 -or
    $dashboard.recentActivity.Count -gt 10 -or
    $dashboard.overdueLimit -ne 5 -or $dashboard.activityLimit -ne 10) {
    throw 'Dashboard list bounds do not match the server-owned contract.'
}
if (-not (@($dashboard.recentActivity | Where-Object customerName -eq $marker).Count)) {
    throw 'Recent activity did not include the new tenant-owned invoice activity.'
}

$forbidden = Invoke-AllowHttpError `
    "$BaseUri/api/v1/businesses/$businessId/dashboard" $otherOwner.Session
if ($forbidden.StatusCode -ne 404) { throw 'Cross-tenant dashboard access did not return 404.' }
$otherDashboard = Invoke-RestMethod `
    "$BaseUri/api/v1/businesses/$otherBusinessId/dashboard" `
    -WebSession $otherOwner.Session
$otherJson = $otherDashboard | ConvertTo-Json -Depth 10 -Compress
foreach ($id in @($draft.Id, $partialDraft.Id, $overdueDraft.Id, $usdInvoiceId)) {
    if ($otherJson.Contains($id.ToString())) { throw 'Other tenant dashboard leaked a Phase 9 invoice ID.' }
}

$indexSql = @"
SELECT count(*) FROM pg_indexes
WHERE schemaname = 'public' AND indexname IN (
  'IX_invoices_BusinessId_UpdatedAtUtc_Id',
  'IX_audit_entries_BusinessId_OccurredAtUtc_Id');
"@
$indexCount = ($indexSql | docker exec -i $DatabaseContainer `
    psql -U invoice -d invoice -tA).Trim()
if ($indexCount -ne '2') { throw 'Phase 9 query indexes are not both applied.' }

[pscustomobject]@{
    phase=9
    marker=$marker
    filteredInvoices=$list.totalCount
    stableAmountOrder=$true
    overdueFilterCount=$overdueList.totalCount
    pageSize=$page.pageSize
    totalPages=$page.totalPages
    invalidQueriesRejected=5
    dashboardTotal=$dashboard.counts.total
    dashboardOverdue=$dashboard.counts.overdue
    zarOutstandingDelta=$zarDelta
    usdOutstandingDelta=$usdDelta
    overdueRows=$dashboard.overdueInvoices.Count
    recentActivityRows=$dashboard.recentActivity.Count
    crossTenantStatus=$forbidden.StatusCode
    queryIndexes=[int]$indexCount
} | ConvertTo-Json -Depth 6
