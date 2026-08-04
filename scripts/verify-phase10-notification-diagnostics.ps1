[CmdletBinding()]
param(
    [string]$ProjectName = 'business-invoice-tracker'
)

$ErrorActionPreference = 'Stop'
$automationContainer = "$ProjectName-automation-1"
$postgresContainer = "$ProjectName-postgres-1"
$events = @()
docker logs $automationContainer 2>&1 | ForEach-Object {
    try {
        $event = ([string]$_) | ConvertFrom-Json
        if ($event.EventId -eq 8202 -and
            $event.State.Outcome -eq 'PermanentFailure') {
            $events += $event
        }
    }
    catch {
        # Non-JSON runtime preamble is not diagnostic evidence.
    }
}
if ($LASTEXITCODE -ne 0) { throw 'Could not read automation JSON logs.' }
if ($events.Count -eq 0) {
    throw 'No permanent notification failure exists in the current automation logs.'
}

$failure = $events[-1]
$traceId = @($failure.Scopes | Where-Object { $_.TraceId } | Select-Object -First 1).TraceId
$workflowId = [string]$failure.State.WorkflowId
$deliveryId = [string]$failure.State.DeliveryId
if ([string]::IsNullOrWhiteSpace($traceId) -or
    [string]::IsNullOrWhiteSpace($workflowId) -or
    [string]::IsNullOrWhiteSpace($deliveryId)) {
    throw 'The failure log does not contain trace, workflow and delivery correlation.'
}

$sql = @"
SELECT json_build_object(
  'status', to_jsonb(d)->>'Status',
  'attempts', (to_jsonb(d)->>'Attempts')::int,
  'permanentFailureAttempts', (
    SELECT count(*)
    FROM notification_attempts a
    WHERE to_jsonb(a)->>'DeliveryId' = to_jsonb(d)->>'Id'
      AND to_jsonb(a)->>'Outcome' = 'PermanentFailure'))
FROM notification_deliveries d
WHERE to_jsonb(d)->>'Id' = '$deliveryId';
"@
$json = $sql | docker exec -i $postgresContainer `
    psql -U invoice -d invoice -tA
if ($LASTEXITCODE -ne 0) { throw 'Could not query durable notification evidence.' }
$delivery = ([string]$json).Trim() | ConvertFrom-Json
if ($delivery.permanentFailureAttempts -lt 1) {
    throw 'The correlated permanent failure has no durable attempt record.'
}

[pscustomobject]@{
    traceId = $traceId
    workflowId = $workflowId
    deliveryId = $deliveryId
    loggedOutcome = $failure.State.Outcome
    loggedAttempts = [int]$failure.State.Attempts
    durableCurrentStatus = $delivery.status
    durableTotalAttempts = $delivery.attempts
    durablePermanentFailureAttempts = $delivery.permanentFailureAttempts
} | ConvertTo-Json -Depth 5
