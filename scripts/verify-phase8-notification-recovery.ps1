[CmdletBinding()]
param(
    [string]$BaseUri = 'http://127.0.0.1:8080',
    [string]$AutomationUri = 'http://127.0.0.1:5191',
    [string]$MailpitUri = 'http://127.0.0.1:8025',
    [string]$DatabaseContainer = 'business-invoice-tracker-postgres-1'
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
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

function Set-Chaos([int]$Code, [int]$Probability) {
    $body = @{
        Recipient=@{ErrorCode=$Code;Probability=$Probability}
    } | ConvertTo-Json -Depth 4
    $null = Invoke-RestMethod "$MailpitUri/api/v1/chaos" `
        -Method Put -ContentType 'application/json' -Body $body
}

function New-PastDueInvoice([string]$Description) {
    $invoiceId = [guid]::NewGuid()
    $today = (Get-Date).ToUniversalTime().Date
    $body = @{
        requestId=$invoiceId; customerId=$customerId; currency='ZAR'
        issueDate=$today.AddDays(-2).ToString('yyyy-MM-dd')
        dueDate=$today.AddDays(-1).ToString('yyyy-MM-dd')
        lines=@(@{description=$Description;quantity=1;unitPrice=100;taxRate=15})
    } | ConvertTo-Json -Depth 8
    $draft = Invoke-RestMethod "$BaseUri/api/v1/businesses/$businessId/invoices" `
        -Method Post -WebSession $session -Headers $headers `
        -ContentType 'application/json' -Body $body
    $issueId = [guid]::NewGuid()
    $null = Invoke-RestMethod `
        "$BaseUri/api/v1/businesses/$businessId/invoices/$invoiceId/issue" `
        -Method Post -WebSession $session -Headers $headers `
        -ContentType 'application/json' `
        -Body (@{requestId=$issueId;version=$draft.version} | ConvertTo-Json)
    return $invoiceId
}

function Get-Delivery([guid]$InvoiceId) {
    $sql = @"
SELECT json_build_object(
  'id', to_jsonb(d)->>'Id',
  'status', to_jsonb(d)->>'Status',
  'attempts', (to_jsonb(d)->>'Attempts')::int,
  'outcomes', (SELECT json_agg(to_jsonb(a)->>'Outcome' ORDER BY (to_jsonb(a)->>'AttemptNumber')::int) FROM notification_attempts a WHERE to_jsonb(a)->>'DeliveryId' = to_jsonb(d)->>'Id'))
FROM notification_deliveries d
WHERE to_jsonb(d)->>'InvoiceId' = '$InvoiceId';
"@
    $json = $sql | docker exec -i $DatabaseContainer psql -U invoice -d invoice -tA
    if ($LASTEXITCODE -ne 0) { throw 'Notification evidence query failed.' }
    $trimmed = if ($null -eq $json) { '' } else { ([string]$json).Trim() }
    return $(if ($trimmed) { $trimmed | ConvertFrom-Json } else { $null })
}

try {
    Set-Chaos 451 100
    $transientInvoice = New-PastDueInvoice 'Phase 8 transient and worker restart proof'
    $deadline = (Get-Date).AddSeconds(45)
    do {
        Start-Sleep -Milliseconds 100
        $transient = Get-Delivery $transientInvoice
    } until (($null -ne $transient -and
        $transient.outcomes -contains 'TransientFailure') -or
        (Get-Date) -ge $deadline)
    if ($null -eq $transient -or
        $transient.outcomes -notcontains 'TransientFailure') {
        throw 'A 451 recipient response did not produce a transient attempt.'
    }

    Set-Chaos 451 0
    docker compose --env-file .env.example up -d --force-recreate `
        automation automation-dapr --wait | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Automation worker restart failed.' }

    $deadline = (Get-Date).AddSeconds(60)
    do {
        Start-Sleep -Milliseconds 250
        $transient = Get-Delivery $transientInvoice
    } until (($null -ne $transient -and $transient.status -eq 'Delivered') -or
        (Get-Date) -ge $deadline)
    if ($transient.status -ne 'Delivered' -or
        $transient.outcomes[0] -ne 'TransientFailure' -or
        $transient.outcomes[-1] -ne 'Delivered' -or
        $transient.attempts -gt 3) {
        throw "Transient recovery evidence is inconsistent: $($transient | ConvertTo-Json -Compress)"
    }

    Set-Chaos 550 100
    $permanentInvoice = New-PastDueInvoice 'Phase 8 permanent invalid-recipient proof'
    $deadline = (Get-Date).AddSeconds(45)
    do {
        Start-Sleep -Milliseconds 250
        $permanent = Get-Delivery $permanentInvoice
    } until (($null -ne $permanent -and $permanent.status -eq 'Failed') -or
        (Get-Date) -ge $deadline)
    if ($permanent.status -ne 'Failed' -or $permanent.attempts -ne 1 -or
        $permanent.outcomes.Count -ne 1 -or
        $permanent.outcomes[0] -ne 'PermanentFailure') {
        throw "Permanent failure evidence is inconsistent: $($permanent | ConvertTo-Json -Compress)"
    }

    Set-Chaos 550 0
    Start-Sleep -Seconds 8
    $permanentAfterWait = Get-Delivery $permanentInvoice
    if ($permanentAfterWait.attempts -ne 1 -or
        $permanentAfterWait.status -ne 'Failed') {
        throw 'Permanent invalid-recipient failure retried after reaching terminal state.'
    }

    $manualRetryId = [guid]::NewGuid()
    $manualResponse = Invoke-RestMethod `
        "$AutomationUri/internal/notification-deliveries/$($permanentAfterWait.id)/retry" `
        -Method Post -ContentType 'application/json' `
        -Body (@{requestId=$manualRetryId} | ConvertTo-Json)
    if ($manualResponse.status -ne 'Accepted') {
        throw 'Manual notification retry was not durably accepted.'
    }
    $deadline = (Get-Date).AddSeconds(45)
    do {
        Start-Sleep -Milliseconds 250
        $manual = Get-Delivery $permanentInvoice
    } until ($manual.status -eq 'Delivered' -or (Get-Date) -ge $deadline)
    if ($manual.status -ne 'Delivered' -or $manual.attempts -ne 2 -or
        $manual.outcomes[-1] -ne 'Delivered') {
        throw "Manual retry did not recover: $($manual | ConvertTo-Json -Compress)"
    }

    [pscustomobject]@{
        phase=8
        transientInvoiceId=$transientInvoice
        transientStatus=$transient.status
        transientAttempts=$transient.attempts
        transientOutcomes=$transient.outcomes
        workerRestartedDuringRetry=$true
        permanentInvoiceId=$permanentInvoice
        permanentStatus=$permanentAfterWait.status
        permanentAttempts=$permanentAfterWait.attempts
        permanentOutcomes=$permanentAfterWait.outcomes
        boundedNoRetryWindowSeconds=8
        manualRetryRequestId=$manualRetryId
        manualRetryFinalStatus=$manual.status
        manualRetryTotalAttempts=$manual.attempts
    } | ConvertTo-Json -Depth 5
}
finally {
    try { Set-Chaos 451 0 } catch { Write-Warning $_ }
}
