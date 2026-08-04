# Phase 10 Operations Runbook

This runbook is for the local assessment topology. The automation management
surface is bound to loopback and is not an authenticated production management
plane.

## First response

```powershell
Invoke-RestMethod http://127.0.0.1:8080/health/ready | ConvertTo-Json -Depth 5
Invoke-RestMethod http://127.0.0.1:5191/health/ready | ConvertTo-Json -Depth 5
Invoke-RestMethod http://127.0.0.1:5191/internal/operations/status | ConvertTo-Json -Depth 5
docker compose --env-file .env.example ps
```

`/health/live` answers only whether the process can serve HTTP. API readiness
requires PostgreSQL and its Dapr sidecar. Automation readiness additionally
requires placement and scheduler; Mailpit loss reports `degraded` with HTTP 200
because durable intake and workflow truth can continue.

The operational snapshot is refreshed every 10 seconds and explicitly reports
`isStale`, `lastSuccessfulRefreshAtUtc`, and an exception type if PostgreSQL
cannot be queried. Metric callbacks read only this cached snapshot.

## Inventory and thresholds

The local defaults below are diagnostic thresholds, not demonstrated service
level objectives:

| Signal | Investigate when | First evidence |
|---|---:|---|
| `invoice.outbox.pending` | nonzero and oldest age > 60s | API/Dapr/Redis health, then outbox error |
| `invoice.domain.events.retained` | unexpected change in retained history | bounded type-level inventory and product event creation |
| `invoice.workflow.commands.pending` | nonzero and age/retries keep increasing | automation Dapr, placement, scheduler |
| `invoice.workflow.commands.failed` | > 0 | command kind, attempts, last error |
| `invoice.automation.deadletters.unresolved` | > 0 | event type/message ID and subscriber logs |
| `invoice.notifications.pending` | nonzero beyond one retry cycle | workflow and Mailpit health |
| `invoice.notifications.failed` | > 0 | delivery attempts and correlated failure log |
| `invoice.operations.snapshot.stale` | 1 | PostgreSQL connectivity and monitor Event 8301 |

The current assessment topology intentionally has no external consumer for
`InvoiceBecameOverdue`. Those durable records remain inspectable as domain-event
history but are excluded from `invoice.outbox.pending` and its oldest-age gauge.
They are reported separately by `invoice.domain.events.retained` and
`retainedDomainEventCount`; an outbox backlog therefore means work expected to be
published, while retained history never masquerades as a transport failure.

Use these bounded SQL queries if the metrics viewer or loopback endpoint is not
available:

```powershell
@'
SELECT "Id", "Type", "OccurredAtUtc", "PublishAttempts",
       "NextAttemptAtUtc", left(coalesce("LastError", ''), 200) AS error
FROM outbox_messages
WHERE "PublishedAtUtc" IS NULL
ORDER BY "OccurredAtUtc", "Id"
LIMIT 50;

SELECT "Id", "Kind", "CreatedAtUtc", "Attempts", "FailedAtUtc",
       left(coalesce("LastError", ''), 200) AS error
FROM workflow_commands
WHERE "DispatchedAtUtc" IS NULL OR "FailedAtUtc" IS NOT NULL
ORDER BY "CreatedAtUtc", "Id"
LIMIT 50;

SELECT "MessageId", "EventType", "ReceivedAtUtc"
FROM automation_dead_letters
WHERE "ResolvedAtUtc" IS NULL
ORDER BY "ReceivedAtUtc", "MessageId"
LIMIT 50;

SELECT "Id", "InvoiceId", "Status", "Attempts", "LastAttemptAtUtc",
       left(coalesce("LastError", ''), 200) AS error
FROM notification_deliveries
WHERE "Status" IN ('Pending', 'Failed')
ORDER BY "CreatedAtUtc", "Id"
LIMIT 50;
'@ | docker exec -i business-invoice-tracker-postgres-1 `
    psql -U invoice -d invoice
```

Do not delete or directly mark these rows successful. Repair the unavailable
dependency and allow the owning durable loop to recover. A failed notification
has an explicit, idempotent manual retry endpoint exercised by Phase 8.

## Notification correlation

Event 8202 contains `TraceId`, `WorkflowId`, `DeliveryId`, outcome and attempt
count. Event 8203 is a transient attempt and carries the same workflow/delivery
identifiers. Follow one failure with:

```powershell
docker logs business-invoice-tracker-automation-1 2>&1 |
    Select-String '8202|8203|<trace-or-workflow-or-delivery-id>'
.\scripts\verify-phase10-notification-diagnostics.ps1
```

The diagnostic verifier joins that JSON log correlation to the durable attempt
row. Invoice, workflow, delivery and message IDs are allowed in logs/traces but
never metric labels. Customer names, email addresses, cookies, antiforgery
tokens, passwords and message bodies must not be added to telemetry.

## Dependency recovery

- PostgreSQL: keep both processes running, restore PostgreSQL, wait for readiness,
  then confirm the snapshot is no longer stale. EF retries only declared transient
  database failures; durable publishers retain their rows.
- Redis: restore Redis and inspect outbox age/attempts. One outbox lease performs
  one publish call; the delay grows to a 30-second cap.
- API/automation Dapr: restore the matching sidecar. With shared network
  namespaces, replace an app and sidecar as a pair when recreating containers.
- Placement/scheduler: restore the control plane, wait for automation readiness,
  then inspect pending/failed workflow commands and workflow state.
- Mailpit: restore it and inspect notification attempts. Workflow alone owns the
  three-attempt delivery cycle; never add an SMTP retry loop.
- Web/API boundary: static nginx can remain available while API calls fail. Do
  not report a mutation as successful without a validated API response.

Run the restoration-safe failure matrix with:

```powershell
.\scripts\verify-phase10.ps1
```

## Optional telemetry viewer

```powershell
docker compose --env-file .env.example -f compose.yaml `
  -f compose.observability.yaml --profile observability up -d
```

In Aspire, filter resources to `invoice-api` and `invoice-automation`, then use
the metric names in the table and traces named `outbox.publish`,
`workflow.command.dispatch`, and `notification.deliver`. The viewer is local,
unauthenticated, in-memory and optional. If it fails, use JSON stdout plus the
operator endpoint/SQL queries; business readiness must remain healthy.
