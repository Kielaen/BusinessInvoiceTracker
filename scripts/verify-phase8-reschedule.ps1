[CmdletBinding()]
param(
    [string]$BaseUri = 'http://127.0.0.1:8080',
    [string]$DatabaseContainer = 'business-invoice-tracker-postgres-1'
)

$ErrorActionPreference = 'Stop'
$businessId = '11111111-1111-4111-8111-111111111111'
$customerId = '50000000-0000-4000-8000-000000000001'
$session = New-Object Microsoft.PowerShell.Commands.WebRequestSession
$token = Invoke-RestMethod "$BaseUri/api/v1/auth/antiforgery" -WebSession $session
$headers = @{}; $headers[$token.headerName] = $token.token
$login = @{email='owner@acme.test';password='Phase2-local-only!'} | ConvertTo-Json
$null = Invoke-RestMethod "$BaseUri/api/v1/auth/login" -Method Post `
    -WebSession $session -Headers $headers -ContentType 'application/json' -Body $login
$token = Invoke-RestMethod "$BaseUri/api/v1/auth/antiforgery" -WebSession $session
$headers = @{}; $headers[$token.headerName] = $token.token

$invoiceId = [guid]::NewGuid()
$today = (Get-Date).ToUniversalTime().Date
$draftBody = @{
    requestId=$invoiceId; customerId=$customerId; currency='ZAR'
    issueDate=$today.AddDays(-2).ToString('yyyy-MM-dd')
    dueDate=$today.AddDays(1).ToString('yyyy-MM-dd')
    lines=@(@{description='Phase 8 due-date reschedule proof';quantity=1;unitPrice=100;taxRate=15})
} | ConvertTo-Json -Depth 8
$draft = Invoke-RestMethod "$BaseUri/api/v1/businesses/$businessId/invoices" `
    -Method Post -WebSession $session -Headers $headers `
    -ContentType 'application/json' -Body $draftBody
$issueId = [guid]::NewGuid()
$issued = Invoke-RestMethod `
    "$BaseUri/api/v1/businesses/$businessId/invoices/$invoiceId/issue" `
    -Method Post -WebSession $session -Headers $headers `
    -ContentType 'application/json' `
    -Body (@{requestId=$issueId;version=$draft.version} | ConvertTo-Json)
if ($issued.status -ne 'Issued') { throw 'Invoice did not enter Issued.' }

$startDeadline = (Get-Date).AddSeconds(30)
do {
    Start-Sleep -Milliseconds 250
    $startSql = "SELECT count(*) FROM workflow_commands WHERE to_jsonb(workflow_commands)->>'InvoiceId' = '$invoiceId' AND to_jsonb(workflow_commands)->>'Kind' = 'Start' AND to_jsonb(workflow_commands)->>'DispatchedAtUtc' IS NOT NULL;"
    $startCount = ($startSql | docker exec -i $DatabaseContainer `
        psql -U invoice -d invoice -tA).Trim()
} until ($startCount -eq '1' -or (Get-Date) -ge $startDeadline)
if ($startCount -ne '1') { throw 'The initial workflow start was not dispatched.' }

docker compose --env-file .env.example stop automation automation-dapr | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Could not stop the workflow worker during its timer.' }
docker compose --env-file .env.example start automation automation-dapr | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Could not restart the workflow worker.' }
$readyDeadline = (Get-Date).AddSeconds(45)
do {
    try {
        $ready = (Invoke-WebRequest 'http://127.0.0.1:5191/health/ready' `
            -UseBasicParsing).StatusCode
    }
    catch { $ready = 0 }
    if ($ready -ne 200) { Start-Sleep -Milliseconds 500 }
} until ($ready -eq 200 -or (Get-Date) -ge $readyDeadline)
if ($ready -ne 200) { throw 'Workflow worker did not become ready after restart.' }

$changeId = [guid]::NewGuid()
$newDueDate = $today.AddDays(-1).ToString('yyyy-MM-dd')
$changeBody = @{
    requestId=$changeId; version=$issued.version; dueDate=$newDueDate
} | ConvertTo-Json
$changed = Invoke-RestMethod `
    "$BaseUri/api/v1/businesses/$businessId/invoices/$invoiceId/due-date" `
    -Method Post -WebSession $session -Headers $headers `
    -ContentType 'application/json' -Body $changeBody
$replay = Invoke-RestMethod `
    "$BaseUri/api/v1/businesses/$businessId/invoices/$invoiceId/due-date" `
    -Method Post -WebSession $session -Headers $headers `
    -ContentType 'application/json' -Body $changeBody
if ($changed.dueDate -ne $newDueDate -or $replay.version -ne $changed.version) {
    throw 'Due-date change or its idempotent replay was inconsistent.'
}

$deadline = (Get-Date).AddSeconds(60)
do {
    Start-Sleep -Milliseconds 500
    $current = Invoke-RestMethod `
        "$BaseUri/api/v1/businesses/$businessId/invoices/$invoiceId" `
        -WebSession $session
} until ($current.status -eq 'Overdue' -or (Get-Date) -ge $deadline)
if ($current.status -ne 'Overdue') {
    throw "Invoice remained $($current.status) after the due-date signal."
}

$evidenceSql = @"
SELECT json_build_object(
  'changeOutbox', (SELECT count(*) FROM outbox_messages o WHERE to_jsonb(o)->>'Id' = '$changeId' AND to_jsonb(o)->>'Type' = 'InvoiceDueDateChanged'),
  'changeAudit', (SELECT count(*) FROM audit_entries a WHERE to_jsonb(a)->>'AggregateId' = '$invoiceId' AND to_jsonb(a)->>'Action' = 'InvoiceDueDateChanged'),
  'changeCommand', (SELECT count(*) FROM workflow_commands w WHERE to_jsonb(w)->>'EventMessageId' = '$changeId' AND to_jsonb(w)->>'DispatchedAtUtc' IS NOT NULL),
  'overdueAudit', (SELECT count(*) FROM audit_entries a WHERE to_jsonb(a)->>'AggregateId' = '$invoiceId' AND to_jsonb(a)->>'Action' = 'InvoiceBecameOverdue'),
  'deliveries', (SELECT count(*) FROM notification_deliveries d WHERE to_jsonb(d)->>'InvoiceId' = '$invoiceId' AND to_jsonb(d)->>'Status' = 'Delivered'));
"@
$evidenceDeadline = (Get-Date).AddSeconds(60)
do {
    $evidenceJson = $evidenceSql | docker exec -i $DatabaseContainer `
        psql -U invoice -d invoice -tA
    if ($LASTEXITCODE -ne 0) { throw 'Due-date evidence query failed.' }
    $evidence = $evidenceJson.Trim() | ConvertFrom-Json
    if ($evidence.deliveries -ne 1) { Start-Sleep -Milliseconds 250 }
} until ($evidence.deliveries -eq 1 -or (Get-Date) -ge $evidenceDeadline)
if ($evidence.changeOutbox -ne 1 -or $evidence.changeAudit -ne 1 -or
    $evidence.changeCommand -ne 1 -or $evidence.overdueAudit -ne 1 -or
    $evidence.deliveries -ne 1) {
    throw "Due-date evidence is inconsistent: $evidenceJson"
}

[pscustomobject]@{
    phase=8
    proof='due-date-change-while-waiting'
    invoiceId=$invoiceId
    status=$current.status
    dueDate=$current.dueDate
    idempotentReplay=$true
    workerRestartedDuringDurableTimer=$true
    changeOutboxRows=$evidence.changeOutbox
    changeAuditRows=$evidence.changeAudit
    dispatchedChangeCommands=$evidence.changeCommand
    overdueAuditRows=$evidence.overdueAudit
    deliveredNotifications=$evidence.deliveries
} | ConvertTo-Json -Depth 4
