[CmdletBinding()]
param(
    [string]$BaseUri = 'http://127.0.0.1:8080',
    [string]$MailpitUri = 'http://127.0.0.1:8025',
    [string]$DatabaseContainer = 'business-invoice-tracker-postgres-1'
)

$ErrorActionPreference = 'Stop'
$businessId = '11111111-1111-4111-8111-111111111111'
$customerId = '50000000-0000-4000-8000-000000000001'
$session = New-Object Microsoft.PowerShell.Commands.WebRequestSession
$token = Invoke-RestMethod "$BaseUri/api/v1/auth/antiforgery" -WebSession $session
$headers = @{}; $headers[$token.headerName] = $token.token
$loginBody = @{email='owner@acme.test';password='Phase2-local-only!'} | ConvertTo-Json
$null = Invoke-RestMethod "$BaseUri/api/v1/auth/login" -Method Post `
    -WebSession $session -Headers $headers -ContentType 'application/json' `
    -Body $loginBody
$token = Invoke-RestMethod "$BaseUri/api/v1/auth/antiforgery" -WebSession $session
$headers = @{}; $headers[$token.headerName] = $token.token

$invoiceId = [guid]::NewGuid()
$issueDate = (Get-Date).ToUniversalTime().Date.AddDays(-2).ToString('yyyy-MM-dd')
$dueDate = (Get-Date).ToUniversalTime().Date.AddDays(-1).ToString('yyyy-MM-dd')
$draftBody = @{
    requestId=$invoiceId; customerId=$customerId; currency='ZAR'
    issueDate=$issueDate; dueDate=$dueDate
    lines=@(@{description='Phase 8 durable overdue proof';quantity=1;unitPrice=100;taxRate=15})
} | ConvertTo-Json -Depth 8
try {
    $draft = Invoke-RestMethod "$BaseUri/api/v1/businesses/$businessId/invoices" `
        -Method Post -WebSession $session -Headers $headers `
        -ContentType 'application/json' -Body $draftBody
}
catch {
    $response = $_.Exception.Response
    if ($null -eq $response) { throw }
    $reader = New-Object IO.StreamReader($response.GetResponseStream())
    try { $details = $reader.ReadToEnd() } finally { $reader.Dispose() }
    throw "Draft creation failed: $details"
}

$issueId = [guid]::NewGuid()
$issueBody = @{requestId=$issueId;version=$draft.version} | ConvertTo-Json
$issued = Invoke-RestMethod `
    "$BaseUri/api/v1/businesses/$businessId/invoices/$invoiceId/issue" `
    -Method Post -WebSession $session -Headers $headers `
    -ContentType 'application/json' -Body $issueBody
if ($issued.status -ne 'Issued') { throw 'Invoice did not enter Issued.' }

$deadline = (Get-Date).AddSeconds(60)
do {
    Start-Sleep -Milliseconds 500
    $current = Invoke-RestMethod `
        "$BaseUri/api/v1/businesses/$businessId/invoices/$invoiceId" `
        -WebSession $session
} until ($current.status -eq 'Overdue' -or (Get-Date) -ge $deadline)
if ($current.status -ne 'Overdue') {
    throw "Invoice remained $($current.status) after the workflow deadline."
}

$sql = @"
SELECT json_build_object(
  'deliveryId', to_jsonb(d)->>'Id',
  'status', to_jsonb(d)->>'Status',
  'attempts', (to_jsonb(d)->>'Attempts')::int,
  'messageId', to_jsonb(d)->>'ProviderMessageId',
  'attemptRows', (SELECT count(*) FROM notification_attempts a WHERE to_jsonb(a)->>'DeliveryId' = to_jsonb(d)->>'Id'),
  'auditRows', (SELECT count(*) FROM audit_entries a WHERE to_jsonb(a)->>'AggregateId' = '$invoiceId' AND to_jsonb(a)->>'Action' = 'InvoiceBecameOverdue'),
  'overdueOutboxRows', (SELECT count(*) FROM outbox_messages o WHERE to_jsonb(o)->>'Type' = 'InvoiceBecameOverdue' AND to_jsonb(o)->>'Payload' LIKE '%$invoiceId%'))
FROM notification_deliveries d
WHERE to_jsonb(d)->>'InvoiceId' = '$invoiceId';
"@
$deliveryDeadline = (Get-Date).AddSeconds(30)
do {
    Start-Sleep -Milliseconds 250
    $deliveryJson = $sql | docker exec -i $DatabaseContainer `
        psql -U invoice -d invoice -tA
    if ($LASTEXITCODE -ne 0) { throw 'Notification evidence query failed.' }
    $trimmedDelivery = $deliveryJson.Trim()
    $delivery = if ($trimmedDelivery) {
        $trimmedDelivery | ConvertFrom-Json
    } else { $null }
} until (($null -ne $delivery -and $delivery.status -in @('Delivered', 'Failed')) -or
    (Get-Date) -ge $deliveryDeadline)
if ($null -eq $delivery) { throw 'Notification delivery row was not created.' }
if ($delivery.status -ne 'Delivered' -or $delivery.attempts -ne 1 -or
    $delivery.attemptRows -ne 1 -or $delivery.auditRows -ne 1 -or
    $delivery.overdueOutboxRows -ne 1) {
    throw "Durable delivery evidence is inconsistent: $deliveryJson"
}

$normalizedMessageId = $delivery.messageId.Trim('<', '>')
$query = [Uri]::EscapeDataString("message-id:$normalizedMessageId")
$mailpit = Invoke-RestMethod "$MailpitUri/api/v1/search?query=$query"
$mailpitJson = $mailpit | ConvertTo-Json -Depth 20 -Compress
if (-not $mailpitJson.Contains($normalizedMessageId)) {
    throw "Mailpit did not return stable Message-ID $($delivery.messageId)."
}

$originalPayloadSql = "SELECT `"Payload`"::text FROM outbox_messages WHERE `"Id`" = '$issueId'::uuid;"
$originalPayloadJson = $originalPayloadSql | docker exec -i $DatabaseContainer `
    psql -U invoice -d invoice -tA
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($originalPayloadJson)) {
    throw 'Could not load the original issued event payload.'
}
$originalPayload = $originalPayloadJson.Trim() | ConvertFrom-Json
$inboxDeadline = (Get-Date).AddSeconds(30)
do {
    Start-Sleep -Milliseconds 250
    $inboxSql = "SELECT count(*) FROM inbox_messages WHERE to_jsonb(inbox_messages)->>'MessageId' = '$issueId';"
    $originalInboxCount = ($inboxSql | docker exec -i $DatabaseContainer `
        psql -U invoice -d invoice -tA).Trim()
} until ($originalInboxCount -eq '1' -or (Get-Date) -ge $inboxDeadline)
if ($originalInboxCount -ne '1') {
    throw 'The original issued event did not reach the inbox before duplicate replay.'
}

$duplicateEvent = @{
    specversion='1.0'; id=$issueId.ToString(); source='/phase8-verifier'
    type='InvoiceIssued'; datacontenttype='application/json'
    time=(Get-Date).ToUniversalTime().ToString('o')
    data=$originalPayload
} | ConvertTo-Json -Depth 8
1..2 | ForEach-Object {
    $duplicateResponse = Invoke-WebRequest `
        'http://127.0.0.1:5191/events/invoice-lifecycle' -Method Post `
        -ContentType 'application/cloudevents+json' -Body $duplicateEvent `
        -UseBasicParsing
    if ($duplicateResponse.StatusCode -notin @(200, 204)) {
        throw "Duplicate lifecycle replay returned $($duplicateResponse.StatusCode)."
    }
}
$dedupeSql = "SELECT json_build_object('inbox', (SELECT count(*) FROM inbox_messages i WHERE to_jsonb(i)->>'MessageId' = '$issueId'), 'commands', (SELECT count(*) FROM workflow_commands w WHERE to_jsonb(w)->>'EventMessageId' = '$issueId'));"
$dedupeJson = $dedupeSql | docker exec -i $DatabaseContainer `
    psql -U invoice -d invoice -tA
if ($LASTEXITCODE -ne 0) { throw 'Duplicate-delivery evidence query failed.' }
$dedupe = $dedupeJson.Trim() | ConvertFrom-Json
if ($dedupe.inbox -ne 1 -or $dedupe.commands -ne 1) {
    throw "Duplicate delivery changed durable cardinality: $dedupeJson"
}

[pscustomobject]@{
    phase=8
    invoiceId=$invoiceId
    invoiceNumber=$current.number
    status=$current.status
    deliveryId=$delivery.deliveryId
    deliveryStatus=$delivery.status
    deliveryAttempts=$delivery.attempts
    attemptRows=$delivery.attemptRows
    auditRows=$delivery.auditRows
    overdueOutboxRows=$delivery.overdueOutboxRows
    messageId=$delivery.messageId
    mailpitMatched=$true
    duplicateReplays=2
    inboxRows=$dedupe.inbox
    workflowCommandRows=$dedupe.commands
} | ConvertTo-Json -Depth 5
