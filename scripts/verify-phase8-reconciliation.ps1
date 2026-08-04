[CmdletBinding()]
param(
    [string]$BaseUri = 'http://127.0.0.1:8080',
    [string]$AutomationUri = 'http://127.0.0.1:5191',
    [string]$DaprUri = 'http://127.0.0.1:5192',
    [string]$MailpitUri = 'http://127.0.0.1:8025',
    [string]$DatabaseContainer = 'business-invoice-tracker-postgres-1'
)

$ErrorActionPreference = 'Stop'
$businessId = '11111111-1111-4111-8111-111111111111'
$customerId = '50000000-0000-4000-8000-000000000001'
$session = New-Object Microsoft.PowerShell.Commands.WebRequestSession
$token = Invoke-RestMethod "$BaseUri/api/v1/auth/antiforgery" -WebSession $session
$headers = @{}; $headers[$token.headerName] = $token.token
$null = Invoke-RestMethod "$BaseUri/api/v1/auth/login" -Method Post `
    -WebSession $session -Headers $headers -ContentType 'application/json' `
    -Body (@{email='owner@acme.test';password='Phase2-local-only!'} | ConvertTo-Json)
$token = Invoke-RestMethod "$BaseUri/api/v1/auth/antiforgery" -WebSession $session
$headers = @{}; $headers[$token.headerName] = $token.token

function New-IssuedInvoice([int]$DueOffset, [string]$Description) {
    $invoiceId = [guid]::NewGuid()
    $today = (Get-Date).ToUniversalTime().Date
    $draft = Invoke-RestMethod "$BaseUri/api/v1/businesses/$businessId/invoices" `
        -Method Post -WebSession $session -Headers $headers -ContentType 'application/json' `
        -Body (@{
            requestId=$invoiceId;customerId=$customerId;currency='ZAR'
            issueDate=$today.AddDays(-2).ToString('yyyy-MM-dd')
            dueDate=$today.AddDays($DueOffset).ToString('yyyy-MM-dd')
            lines=@(@{description=$Description;quantity=1;unitPrice=100;taxRate=15})
        } | ConvertTo-Json -Depth 8)
    $null = Invoke-RestMethod `
        "$BaseUri/api/v1/businesses/$businessId/invoices/$invoiceId/issue" `
        -Method Post -WebSession $session -Headers $headers -ContentType 'application/json' `
        -Body (@{requestId=[guid]::NewGuid();version=$draft.version} | ConvertTo-Json)
    return $invoiceId
}

$workflowInvoice = New-IssuedInvoice 1 'Phase 8 missing workflow reconciliation proof'
$workflowId = "invoice-$($workflowInvoice.ToString('N'))-overdue-v1"
$workflowUri = "$DaprUri/v1.0/workflows/dapr/$workflowId"
$deadline = (Get-Date).AddSeconds(45)
do {
    Start-Sleep -Milliseconds 250
    try { $workflow = Invoke-RestMethod $workflowUri } catch { $workflow = $null }
} until (($null -ne $workflow -and $workflow.runtimeStatus -eq 'RUNNING') -or
    (Get-Date) -ge $deadline)
if ($workflow.runtimeStatus -ne 'RUNNING') { throw 'Workflow did not start.' }

$response = Invoke-WebRequest "$workflowUri/terminate" -Method Post -UseBasicParsing
if ($response.StatusCode -ne 202) { throw 'Workflow termination was not accepted.' }
$deadline = (Get-Date).AddSeconds(20)
do {
    Start-Sleep -Milliseconds 200
    $workflow = Invoke-RestMethod $workflowUri
} until ($workflow.runtimeStatus -eq 'TERMINATED' -or (Get-Date) -ge $deadline)
if ($workflow.runtimeStatus -ne 'TERMINATED') { throw 'Workflow did not terminate.' }
$response = Invoke-WebRequest "$workflowUri/purge" -Method Post -UseBasicParsing
if ($response.StatusCode -ne 202) { throw 'Workflow purge was not accepted.' }

$reconciliation = Invoke-RestMethod "$AutomationUri/internal/reconciliation/run" `
    -Method Post
if ($reconciliation.workflowDiscrepancies -lt 1 -or
    $reconciliation.repairedStarts -lt 1) {
    throw "Missing workflow was not repaired: $($reconciliation | ConvertTo-Json -Compress)"
}
$deadline = (Get-Date).AddSeconds(45)
do {
    Start-Sleep -Milliseconds 250
    try { $workflow = Invoke-RestMethod $workflowUri } catch { $workflow = $null }
} until (($null -ne $workflow -and $workflow.runtimeStatus -eq 'RUNNING') -or
    (Get-Date) -ge $deadline)
if ($workflow.runtimeStatus -ne 'RUNNING') {
    throw 'Repaired start command did not recreate the workflow.'
}

$deliveryInvoice = New-IssuedInvoice -1 'Phase 8 accepted-before-ack reconciliation proof'
$deadline = (Get-Date).AddSeconds(60)
do {
    Start-Sleep -Milliseconds 250
    $deliverySql = @"
SELECT json_build_object(
  'id', to_jsonb(d)->>'Id',
  'status', to_jsonb(d)->>'Status',
  'messageId', to_jsonb(d)->>'ProviderMessageId')
FROM notification_deliveries d
WHERE to_jsonb(d)->>'InvoiceId' = '$deliveryInvoice';
"@
    $deliveryJson = $deliverySql | docker exec -i $DatabaseContainer `
        psql -U invoice -d invoice -tA
    $trimmed = if ($null -eq $deliveryJson) { '' } else { ([string]$deliveryJson).Trim() }
    $delivery = if ($trimmed) { $trimmed | ConvertFrom-Json } else { $null }
} until (($null -ne $delivery -and $delivery.status -eq 'Delivered') -or
    (Get-Date) -ge $deadline)
if ($delivery.status -ne 'Delivered') { throw 'Baseline delivery was not accepted.' }

$normalized = $delivery.messageId.Trim('<', '>')
$query = [Uri]::EscapeDataString("message-id:$normalized")
$mailpitBefore = Invoke-RestMethod "$MailpitUri/api/v1/search?query=$query"
if ($mailpitBefore.messages_count -ne 1) { throw 'Baseline Mailpit message is missing.' }

$simulateSql = @"
BEGIN;
DELETE FROM notification_attempts WHERE to_jsonb(notification_attempts)->>'DeliveryId' = '$($delivery.id)';
UPDATE notification_deliveries
SET "Status" = 'Pending', "Attempts" = 0, "LastAttemptAtUtc" = NULL,
    "DeliveredAtUtc" = NULL, "LastError" = NULL
WHERE "Id" = '$($delivery.id)'::uuid;
COMMIT;
"@
$null = $simulateSql | docker exec -i $DatabaseContainer `
    psql -U invoice -d invoice -v ON_ERROR_STOP=1
if ($LASTEXITCODE -ne 0) { throw 'Could not simulate the acknowledgment crash window.' }

$deliveryReconciliation = Invoke-RestMethod `
    "$AutomationUri/internal/reconciliation/run" -Method Post
if ($deliveryReconciliation.reconciledDeliveries -lt 1) {
    throw 'Accepted Mailpit message was not reconciled.'
}
$verifySql = @"
SELECT json_build_object(
  'status', to_jsonb(d)->>'Status',
  'attempts', (to_jsonb(d)->>'Attempts')::int,
  'reconciledAttempts', (SELECT count(*) FROM notification_attempts a WHERE to_jsonb(a)->>'DeliveryId' = to_jsonb(d)->>'Id' AND to_jsonb(a)->>'Outcome' = 'Reconciled'))
FROM notification_deliveries d WHERE "Id" = '$($delivery.id)'::uuid;
"@
$verify = ($verifySql | docker exec -i $DatabaseContainer `
    psql -U invoice -d invoice -tA).Trim() | ConvertFrom-Json
$mailpitAfter = Invoke-RestMethod "$MailpitUri/api/v1/search?query=$query"
if ($verify.status -ne 'Delivered' -or $verify.attempts -ne 1 -or
    $verify.reconciledAttempts -ne 1 -or $mailpitAfter.messages_count -ne 1) {
    throw 'Accepted-before-ack recovery duplicated or lost delivery evidence.'
}

[pscustomobject]@{
    phase=8
    missingWorkflowInvoiceId=$workflowInvoice
    purgedWorkflowRecreated=$true
    workflowDiscrepancies=$reconciliation.workflowDiscrepancies
    repairedStarts=$reconciliation.repairedStarts
    deliveryInvoiceId=$deliveryInvoice
    acceptedBeforeAckSimulated=$true
    reconciledDeliveries=$deliveryReconciliation.reconciledDeliveries
    deliveryStatus=$verify.status
    reconciliationAttempts=$verify.reconciledAttempts
    mailpitMessageCount=$mailpitAfter.messages_count
} | ConvertTo-Json -Depth 5
