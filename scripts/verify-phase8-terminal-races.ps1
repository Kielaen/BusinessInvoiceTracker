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
$null = Invoke-RestMethod "$BaseUri/api/v1/auth/login" -Method Post `
    -WebSession $session -Headers $headers -ContentType 'application/json' `
    -Body (@{email='owner@acme.test';password='Phase2-local-only!'} | ConvertTo-Json)
$token = Invoke-RestMethod "$BaseUri/api/v1/auth/antiforgery" -WebSession $session
$headers = @{}; $headers[$token.headerName] = $token.token

function Set-Chaos([int]$Code, [int]$Probability) {
    $body = @{Recipient=@{ErrorCode=$Code;Probability=$Probability}} | ConvertTo-Json -Depth 3
    $null = Invoke-RestMethod "$MailpitUri/api/v1/chaos" -Method Put `
        -ContentType 'application/json' -Body $body
}

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
    $issued = Invoke-RestMethod `
        "$BaseUri/api/v1/businesses/$businessId/invoices/$invoiceId/issue" `
        -Method Post -WebSession $session -Headers $headers -ContentType 'application/json' `
        -Body (@{requestId=[guid]::NewGuid();version=$draft.version} | ConvertTo-Json)
    return [pscustomobject]@{Id=$invoiceId;Invoice=$issued}
}

function Get-Invoice([guid]$InvoiceId) {
    Invoke-RestMethod "$BaseUri/api/v1/businesses/$businessId/invoices/$InvoiceId" `
        -WebSession $session
}

function Pay-In-Full([guid]$InvoiceId, $Invoice) {
    Invoke-RestMethod "$BaseUri/api/v1/businesses/$businessId/invoices/$InvoiceId/payments" `
        -Method Post -WebSession $session -Headers $headers -ContentType 'application/json' `
        -Body (@{
            requestId=[guid]::NewGuid();version=$Invoice.version
            idempotencyKey="phase8-$([guid]::NewGuid())"
            amount=$Invoice.outstandingAmount
            receivedAtUtc=(Get-Date).ToUniversalTime().ToString('o')
        } | ConvertTo-Json)
}

function Get-Delivery([guid]$InvoiceId) {
    $sql = @"
SELECT json_build_object(
  'status', to_jsonb(d)->>'Status',
  'attempts', (to_jsonb(d)->>'Attempts')::int,
  'messageId', to_jsonb(d)->>'ProviderMessageId',
  'outcomes', (SELECT json_agg(to_jsonb(a)->>'Outcome' ORDER BY (to_jsonb(a)->>'AttemptNumber')::int) FROM notification_attempts a WHERE to_jsonb(a)->>'DeliveryId' = to_jsonb(d)->>'Id'))
FROM notification_deliveries d WHERE to_jsonb(d)->>'InvoiceId' = '$InvoiceId';
"@
    $json = $sql | docker exec -i $DatabaseContainer psql -U invoice -d invoice -tA
    $trimmed = if ($null -eq $json) { '' } else { ([string]$json).Trim() }
    return $(if ($trimmed) { $trimmed | ConvertFrom-Json } else { $null })
}

function Wait-ForTransient([guid]$InvoiceId) {
    $deadline = (Get-Date).AddSeconds(45)
    do {
        Start-Sleep -Milliseconds 100
        $delivery = Get-Delivery $InvoiceId
    } until (($null -ne $delivery -and
        $delivery.outcomes -contains 'TransientFailure') -or
        (Get-Date) -ge $deadline)
    if ($null -eq $delivery -or
        $delivery.outcomes -notcontains 'TransientFailure') {
        throw "Invoice $InvoiceId did not reach a transient delivery attempt."
    }
    return $delivery
}

function Wait-ForCancelled([guid]$InvoiceId) {
    $deadline = (Get-Date).AddSeconds(45)
    do {
        Start-Sleep -Milliseconds 200
        $delivery = Get-Delivery $InvoiceId
    } until (($null -ne $delivery -and $delivery.status -eq 'Cancelled') -or
        (Get-Date) -ge $deadline)
    if ($delivery.status -ne 'Cancelled') {
        throw "Invoice $InvoiceId delivery did not cancel: $($delivery | ConvertTo-Json -Compress)"
    }
    return $delivery
}

try {
    docker compose --env-file .env.example stop automation automation-dapr | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Could not stop the automation pair.' }
    $paidBeforeWait = New-IssuedInvoice 1 'Phase 8 paid before workflow wait proof'
    $paidBeforeWaitResult = Pay-In-Full $paidBeforeWait.Id $paidBeforeWait.Invoice
    if ($paidBeforeWaitResult.status -ne 'Paid') { throw 'Invoice was not durably paid.' }
    docker compose --env-file .env.example start automation automation-dapr | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Could not restart the automation pair.' }
    $deadline = (Get-Date).AddSeconds(45)
    do {
        Start-Sleep -Milliseconds 250
        $sql = "SELECT count(*) FROM workflow_commands WHERE to_jsonb(workflow_commands)->>'InvoiceId' = '$($paidBeforeWait.Id)' AND to_jsonb(workflow_commands)->>'DispatchedAtUtc' IS NOT NULL;"
        $dispatched = ($sql | docker exec -i $DatabaseContainer `
            psql -U invoice -d invoice -tA).Trim()
    } until ([int]$dispatched -ge 2 -or (Get-Date) -ge $deadline)
    Start-Sleep -Seconds 3
    if ([int]$dispatched -lt 2 -or $null -ne (Get-Delivery $paidBeforeWait.Id)) {
        throw 'Paid-before-wait did not dispatch both commands without notification.'
    }

    Set-Chaos 451 100
    $paidRace = New-IssuedInvoice -1 'Phase 8 paid versus due timer race proof'
    $null = Wait-ForTransient $paidRace.Id
    $paidRaceCurrent = Get-Invoice $paidRace.Id
    $paidRaceResult = Pay-In-Full $paidRace.Id $paidRaceCurrent
    Set-Chaos 451 0
    $paidDelivery = Wait-ForCancelled $paidRace.Id
    if ($paidRaceResult.status -ne 'Paid' -or $paidDelivery.attempts -ne 1) {
        throw 'Paid race did not terminate with one cancelled delivery attempt.'
    }

    Set-Chaos 451 100
    $voidRace = New-IssuedInvoice -1 'Phase 8 void versus due timer race proof'
    $null = Wait-ForTransient $voidRace.Id
    $voidCurrent = Get-Invoice $voidRace.Id
    $voidResult = Invoke-RestMethod `
        "$BaseUri/api/v1/businesses/$businessId/invoices/$($voidRace.Id)/void" `
        -Method Post -WebSession $session -Headers $headers -ContentType 'application/json' `
        -Body (@{requestId=[guid]::NewGuid();version=$voidCurrent.version} | ConvertTo-Json)
    Set-Chaos 451 0
    $voidDelivery = Wait-ForCancelled $voidRace.Id
    if ($voidResult.status -ne 'Voided' -or $voidDelivery.attempts -ne 1) {
        throw 'Void race did not terminate with one cancelled delivery attempt.'
    }

    foreach ($delivery in @($paidDelivery, $voidDelivery)) {
        $normalized = $delivery.messageId.Trim('<', '>')
        $query = [Uri]::EscapeDataString("message-id:$normalized")
        $mailpit = Invoke-RestMethod "$MailpitUri/api/v1/search?query=$query" |
            ConvertTo-Json -Depth 10 -Compress
        if ($mailpit.Contains($normalized)) {
            throw "Terminal invoice received overdue email $normalized."
        }
    }

    [pscustomobject]@{
        phase=8
        paidBeforeWorkflowWaitInvoiceId=$paidBeforeWait.Id
        paidBeforeWorkflowWaitStatus=$paidBeforeWaitResult.status
        paidBeforeWorkflowWaitCommands=[int]$dispatched
        paidBeforeWorkflowWaitDeliveries=0
        paidRaceInvoiceId=$paidRace.Id
        paidRaceStatus=$paidRaceResult.status
        paidRaceDeliveryStatus=$paidDelivery.status
        paidRaceAttempts=$paidDelivery.attempts
        voidRaceInvoiceId=$voidRace.Id
        voidRaceStatus=$voidResult.status
        voidRaceDeliveryStatus=$voidDelivery.status
        voidRaceAttempts=$voidDelivery.attempts
        terminalEmailsFound=0
    } | ConvertTo-Json -Depth 5
}
finally {
    try { Set-Chaos 451 0 } catch { Write-Warning $_ }
    docker compose --env-file .env.example start automation automation-dapr | Out-Null
}
