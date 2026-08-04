[CmdletBinding()]
param(
    [string]$BaseUri = 'http://127.0.0.1:8080',
    [string]$DatabaseContainer = 'business-invoice-tracker-postgres-1'
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Net.Http
$businessId = '11111111-1111-4111-8111-111111111111'
$customerId = '50000000-0000-4000-8000-000000000001'
$password = 'Phase2-local-only!'

function Invoke-AllowHttpError {
    param([string]$Uri, [Microsoft.PowerShell.Commands.WebRequestSession]$Session,
        [string]$Method = 'Get', [hashtable]$Headers = @{}, [string]$Body)
    try {
        $parameters = @{
            Uri=$Uri; Method=$Method; WebSession=$Session
            Headers=$Headers; UseBasicParsing=$true
        }
        if ($Body) {
            $parameters.ContentType='application/json'
            $parameters.Body=$Body
        }
        return Invoke-WebRequest @parameters
    }
    catch {
        $raw = $_.Exception.Response
        if ($null -eq $raw) { throw }
        $reader = New-Object IO.StreamReader($raw.GetResponseStream())
        try { $content = $reader.ReadToEnd() } finally { $reader.Dispose() }
        return [pscustomobject]@{
            StatusCode=[int]$raw.StatusCode
            Headers=$raw.Headers
            Content=$content
        }
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

function Get-Antiforgery($session) {
    return Invoke-RestMethod "$BaseUri/api/v1/auth/antiforgery" -WebSession $session
}

function Assert-Status($response, [int[]]$expected, [string]$operation) {
    if ($expected -notcontains [int]$response.StatusCode) {
        throw "$operation returned $($response.StatusCode); expected $($expected -join ' or '). Body: $($response.Content)"
    }
}

function New-Draft($session, $headers, [guid]$invoiceId, [string]$description) {
    $body = @{
        requestId=$invoiceId; customerId=$customerId; currency='ZAR'
        issueDate='2026-07-31'; dueDate='2026-08-31'
        lines=@(@{description=$description;quantity=2;unitPrice=100;taxRate=15})
    } | ConvertTo-Json -Depth 8
    $response = Invoke-AllowHttpError `
        "$BaseUri/api/v1/businesses/$businessId/invoices" `
        $session Post $headers $body
    Assert-Status $response @(201) "create $description draft"
    return $response.Content | ConvertFrom-Json
}

function New-ConcurrentPost(
    [string]$uri, [Net.CookieContainer]$cookies, [string]$headerName,
    [string]$headerValue, [string]$body
) {
    $handler = [System.Net.Http.HttpClientHandler]::new()
    $handler.CookieContainer = $cookies
    $client = [System.Net.Http.HttpClient]::new($handler)
    $request = [System.Net.Http.HttpRequestMessage]::new(
        [System.Net.Http.HttpMethod]::Post, $uri)
    $null = $request.Headers.TryAddWithoutValidation($headerName, $headerValue)
    $request.Content = [System.Net.Http.StringContent]::new(
        $body, [Text.Encoding]::UTF8, 'application/json')
    return [pscustomobject]@{
        Client=$client; Request=$request; Task=$client.SendAsync($request)
    }
}

function Complete-ConcurrentPost($pending) {
    $response = $pending.Task.Result
    $content = $response.Content.ReadAsStringAsync().Result
    $result = [pscustomobject]@{
        StatusCode=[int]$response.StatusCode
        Content=$content
    }
    $response.Dispose(); $pending.Request.Dispose(); $pending.Client.Dispose()
    return $result
}

$owner = New-Session 'owner@acme.test'
$token = Get-Antiforgery $owner
$headers = @{}; $headers[$token.headerName] = $token.token

$invoiceAId = [guid]::NewGuid()
$invoiceBId = [guid]::NewGuid()
$invoiceCId = [guid]::NewGuid()
$draftA = New-Draft $owner $headers $invoiceAId 'Phase7 concurrent issue A'
$draftB = New-Draft $owner $headers $invoiceBId 'Phase7 concurrent issue B'
$draftC = New-Draft $owner $headers $invoiceCId 'Phase7 void draft'

$issueAId = [guid]::NewGuid()
$issueBId = [guid]::NewGuid()
$issueABody = @{requestId=$issueAId;version=$draftA.version} | ConvertTo-Json
$issueBBody = @{requestId=$issueBId;version=$draftB.version} | ConvertTo-Json
$issueA = New-ConcurrentPost `
    "$BaseUri/api/v1/businesses/$businessId/invoices/$invoiceAId/issue" `
    $owner.Cookies $token.headerName $token.token $issueABody
$issueB = New-ConcurrentPost `
    "$BaseUri/api/v1/businesses/$businessId/invoices/$invoiceBId/issue" `
    $owner.Cookies $token.headerName $token.token $issueBBody
[Threading.Tasks.Task]::WaitAll(
    [Threading.Tasks.Task[]]@($issueA.Task, $issueB.Task))
$issuedAResponse = Complete-ConcurrentPost $issueA
$issuedBResponse = Complete-ConcurrentPost $issueB
Assert-Status $issuedAResponse @(200) 'concurrent issue A'
Assert-Status $issuedBResponse @(200) 'concurrent issue B'
$issuedA = $issuedAResponse.Content | ConvertFrom-Json
$issuedB = $issuedBResponse.Content | ConvertFrom-Json
if ($issuedA.status -ne 'Issued' -or $issuedB.status -ne 'Issued' -or
    $issuedA.number -eq $issuedB.number -or
    [string]::IsNullOrWhiteSpace($issuedA.number) -or
    [string]::IsNullOrWhiteSpace($issuedB.number)) {
    throw 'Concurrent issue did not allocate two distinct immutable numbers.'
}

$issueReplay = Invoke-AllowHttpError `
    "$BaseUri/api/v1/businesses/$businessId/invoices/$invoiceAId/issue" `
    $owner Post $headers $issueABody
Assert-Status $issueReplay @(200) 'issue replay'
$differentIssue = @{
    requestId=[guid]::NewGuid();version=$issuedA.version
} | ConvertTo-Json
$issueConflict = Invoke-AllowHttpError `
    "$BaseUri/api/v1/businesses/$businessId/invoices/$invoiceAId/issue" `
    $owner Post $headers $differentIssue
Assert-Status $issueConflict @(409) 'second issue command'

$receivedA = (Get-Date).ToUniversalTime().ToString('o')
$paymentAId = [guid]::NewGuid()
$paymentAKey = "phase7-$paymentAId"
$paymentABody = @{
    requestId=$paymentAId;version=$issuedA.version
    idempotencyKey=$paymentAKey;amount=100;receivedAtUtc=$receivedA
} | ConvertTo-Json
$paymentAResponse = Invoke-AllowHttpError `
    "$BaseUri/api/v1/businesses/$businessId/invoices/$invoiceAId/payments" `
    $owner Post $headers $paymentABody
Assert-Status $paymentAResponse @(200) 'partial payment'
$partialA = $paymentAResponse.Content | ConvertFrom-Json
if ($partialA.status -ne 'PartiallyPaid' -or
    [decimal]$partialA.paidAmount -ne 100 -or
    [decimal]$partialA.outstandingAmount -ne 130) {
    throw 'Partial payment totals or transition are incorrect.'
}
$paymentReplay = Invoke-AllowHttpError `
    "$BaseUri/api/v1/businesses/$businessId/invoices/$invoiceAId/payments" `
    $owner Post $headers $paymentABody
Assert-Status $paymentReplay @(200) 'payment replay'
$changedPayment = @{
    requestId=$paymentAId;version=$partialA.version
    idempotencyKey=$paymentAKey;amount=101;receivedAtUtc=$receivedA
} | ConvertTo-Json
$paymentConflict = Invoke-AllowHttpError `
    "$BaseUri/api/v1/businesses/$businessId/invoices/$invoiceAId/payments" `
    $owner Post $headers $changedPayment
Assert-Status $paymentConflict @(409) 'changed payment replay'
$overpay = @{
    requestId=[guid]::NewGuid();version=$partialA.version
    idempotencyKey="overpay-$([guid]::NewGuid())";amount=131
    receivedAtUtc=(Get-Date).ToUniversalTime().ToString('o')
} | ConvertTo-Json
$overpayResponse = Invoke-AllowHttpError `
    "$BaseUri/api/v1/businesses/$businessId/invoices/$invoiceAId/payments" `
    $owner Post $headers $overpay
Assert-Status $overpayResponse @(400) 'overpayment rejection'

$paymentB1Id = [guid]::NewGuid()
$paymentB2Id = [guid]::NewGuid()
$paymentB1 = @{
    requestId=$paymentB1Id;version=$issuedB.version
    idempotencyKey="phase7-$paymentB1Id";amount=150
    receivedAtUtc=(Get-Date).ToUniversalTime().ToString('o')
} | ConvertTo-Json
$paymentB2 = @{
    requestId=$paymentB2Id;version=$issuedB.version
    idempotencyKey="phase7-$paymentB2Id";amount=150
    receivedAtUtc=(Get-Date).ToUniversalTime().AddSeconds(1).ToString('o')
} | ConvertTo-Json
$pendingB1 = New-ConcurrentPost `
    "$BaseUri/api/v1/businesses/$businessId/invoices/$invoiceBId/payments" `
    $owner.Cookies $token.headerName $token.token $paymentB1
$pendingB2 = New-ConcurrentPost `
    "$BaseUri/api/v1/businesses/$businessId/invoices/$invoiceBId/payments" `
    $owner.Cookies $token.headerName $token.token $paymentB2
[Threading.Tasks.Task]::WaitAll(
    [Threading.Tasks.Task[]]@($pendingB1.Task, $pendingB2.Task))
$resultB1 = Complete-ConcurrentPost $pendingB1
$resultB2 = Complete-ConcurrentPost $pendingB2
$paymentStatuses = @($resultB1.StatusCode, $resultB2.StatusCode) | Sort-Object
if (($paymentStatuses -join ',') -ne '200,409') {
    throw "Concurrent payments returned $($paymentStatuses -join ','); expected 200,409."
}
$afterRace = Invoke-RestMethod `
    "$BaseUri/api/v1/businesses/$businessId/invoices/$invoiceBId" `
    -WebSession $owner
if ([decimal]$afterRace.paidAmount -ne 150 -or
    [decimal]$afterRace.outstandingAmount -ne 80) {
    throw 'Concurrent payment race changed the balance more than once.'
}
$winningPaymentId = if ($resultB1.StatusCode -eq 200) { $paymentB1Id } else { $paymentB2Id }
$finalPaymentId = [guid]::NewGuid()
$finalPayment = @{
    requestId=$finalPaymentId;version=$afterRace.version
    idempotencyKey="phase7-$finalPaymentId";amount=80
    receivedAtUtc=(Get-Date).ToUniversalTime().AddSeconds(2).ToString('o')
} | ConvertTo-Json
$paidResponse = Invoke-AllowHttpError `
    "$BaseUri/api/v1/businesses/$businessId/invoices/$invoiceBId/payments" `
    $owner Post $headers $finalPayment
Assert-Status $paidResponse @(200) 'final payment'
$paid = $paidResponse.Content | ConvertFrom-Json
if ($paid.status -ne 'Paid' -or [decimal]$paid.outstandingAmount -ne 0) {
    throw 'Final payment did not transition the invoice to Paid.'
}

$voidPaidBody = @{
    requestId=[guid]::NewGuid();version=$partialA.version
} | ConvertTo-Json
$voidPaid = Invoke-AllowHttpError `
    "$BaseUri/api/v1/businesses/$businessId/invoices/$invoiceAId/void" `
    $owner Post $headers $voidPaidBody
Assert-Status $voidPaid @(400) 'void invoice with payment'
$voidId = [guid]::NewGuid()
$voidBody = @{requestId=$voidId;version=$draftC.version} | ConvertTo-Json
$voidResponse = Invoke-AllowHttpError `
    "$BaseUri/api/v1/businesses/$businessId/invoices/$invoiceCId/void" `
    $owner Post $headers $voidBody
Assert-Status $voidResponse @(200) 'void draft'
$voided = $voidResponse.Content | ConvertFrom-Json
if ($voided.status -ne 'Voided') { throw 'Void did not produce Voided status.' }
$voidReplay = Invoke-AllowHttpError `
    "$BaseUri/api/v1/businesses/$businessId/invoices/$invoiceCId/void" `
    $owner Post $headers $voidBody
Assert-Status $voidReplay @(200) 'void replay'

$viewer = New-Session 'viewer@acme.test'
$viewerToken = Get-Antiforgery $viewer
$viewerHeaders = @{}; $viewerHeaders[$viewerToken.headerName] = $viewerToken.token
foreach ($path in @('issue', 'payments', 'void')) {
    $body = if ($path -eq 'payments') { $paymentABody } else { $issueABody }
    $forbidden = Invoke-AllowHttpError `
        "$BaseUri/api/v1/businesses/$businessId/invoices/$invoiceAId/$path" `
        $viewer Post $viewerHeaders $body
    Assert-Status $forbidden @(403) "viewer $path"
}
$other = New-Session 'owner@other.test'
$crossTenant = Invoke-AllowHttpError `
    "$BaseUri/api/v1/businesses/$businessId/invoices/$invoiceAId" $other
Assert-Status $crossTenant @(404) 'cross-tenant lifecycle read'

$auditA = Invoke-RestMethod `
    "$BaseUri/api/v1/businesses/$businessId/invoices/$invoiceAId/audit" `
    -WebSession $owner
if ('InvoiceIssued' -notin $auditA.items.action -or
    'InvoicePaymentRecorded' -notin $auditA.items.action) {
    throw 'Issue/payment audit history is incomplete.'
}
$commandIds = @(
    $issueAId, $issueBId, $paymentAId, $winningPaymentId,
    $finalPaymentId, $voidId
) | ForEach-Object { "'$($_.ToString())'" }
$sql = "SELECT count(*) FROM outbox_messages WHERE `"Id`" IN ($($commandIds -join ','));"
$outboxCount = $sql | docker exec -i $DatabaseContainer `
    psql -U invoice -d invoice -tA
if ($LASTEXITCODE -ne 0) { throw 'Outbox database assertion failed.' }
if ([int]$outboxCount.Trim() -ne 6) {
    throw "Expected six atomic lifecycle outbox rows; found $outboxCount."
}

[pscustomobject]@{
    phase=7
    gateChecks='concurrent-issue-replay-payment-race-overpay-void-authorization-audit-outbox'
    invoiceNumbers=@($issuedA.number, $issuedB.number)
    partialPaid=$partialA.paidAmount
    raceStatuses=$paymentStatuses
    finalStatus=$paid.status
    voidStatus=$voided.status
    outboxRows=[int]$outboxCount.Trim()
} | ConvertTo-Json -Depth 5
