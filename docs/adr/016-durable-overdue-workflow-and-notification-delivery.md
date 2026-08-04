# ADR-016: Durable overdue workflow and simulated notification delivery

- Status: Accepted for Phase 8 implementation
- Date: 2026-07-31
- Owners: Project maintainers
- Requirements: REQ-010, REQ-011, REQ-017, REQ-019, REQ-020
- Risks: R-016, R-017, R-036, R-038, R-039, R-040, R-041
- Verification: Phase 8 Pub/Sub, workflow, PostgreSQL, Mailpit, restart,
  duplicate, race, retry and reconciliation evidence

## Context

Phase 7 commits stable lifecycle events; Phase 8 extends the publisher only after
the durable consumer boundary exists.
Phase 8 must turn an issued invoice into durable due-date tracking and one
simulated overdue-notification result without treating workflow state as business
truth. The dangerous windows are publish-before-mark, subscriber crash,
event-before-wait, timer/payment races, worker restart, notification acceptance
before database acknowledgement, and poison input.

Dapr's current primary documentation states that Pub/Sub is at-least-once and
that non-success subscriber responses are redelivered. Dead-letter topics require
an explicit resiliency retry policy or a failure is routed immediately. Workflow
activity retry is durable and code-owned, distinct from operator resiliency.
Running workflow instance IDs cannot be reused. Mailpit provides a test API,
Message-ID search, chaos responses and `MP_IGNORE_DUPLICATE_IDS`.

Primary sources:

- https://docs.dapr.io/developing-applications/building-blocks/pubsub/pubsub-overview/
- https://docs.dapr.io/developing-applications/building-blocks/pubsub/pubsub-deadletter/
- https://docs.dapr.io/developing-applications/building-blocks/workflow/workflow-features-concepts/
- https://docs.dapr.io/developing-applications/building-blocks/workflow/howto-manage-workflow/
- https://mailpit.axllent.org/docs/api-v1/
- https://mailpit.axllent.org/docs/usage/search-filters/
- https://mailpit.axllent.org/docs/configuration/runtime-options/

## Decision

### Event publication and consumption

- The API outbox remains the only bridge from the synchronous EF transaction.
  Lifecycle messages publish as CloudEvents to one scoped `invoice-lifecycle`
  topic. A successful Dapr publish is required before `PublishedAtUtc` is set.
- Publisher claims are database leases obtained atomically. Multiple API replicas
  may duplicate after an uncertain publish, but cannot corrupt attempt state or
  concurrently own an unexpired row. Concurrency verification recreates both
  publisher app/sidecar pairs from the same current image before admitting work,
  and structured publication logs include event type and selected topic.
- The automation subscriber first validates the versioned envelope. Its
  PostgreSQL transaction inserts the stable event ID into `InboxMessages` and a
  durable workflow command. Duplicate inbox insertion is a no-op. It acknowledges
  only after that transaction commits.
- Dapr owns bounded transport redelivery: 5 attempts, 2-second constant delay,
  then `invoice-lifecycle-deadletter`. A dead-letter handler persists an
  operator-visible terminal record. It never silently drops a poison message.

### Workflow identity and coordination

- Workflow ID is `invoice-{invoiceId:N}-overdue-v1`. Version is part of the ID;
  a new incompatible orchestration requires `v2`, not in-place history changes.
- A durable workflow-command dispatcher performs schedule/raise operations after
  the inbox transaction and marks the command delivered only after Dapr accepts
  it. The dispatcher may repeat an uncertain call. Existing-instance conflict for
  the same issued invoice is idempotent; external state-change events are allowed
  to repeat because every activity re-reads PostgreSQL.
- The workflow waits for the authoritative `DueAtUtc` timer and external
  `state-changed` events. Paid or Voided ends it. A due-date change causes a
  PostgreSQL re-read and timer replacement. Payment that does not settle the
  invoice leaves tracking active. Events raised before the wait are expected to
  be buffered, but the Phase 8 gate repeats the Phase 0 proof in product code.
- At timer completion, an idempotent activity re-reads the invoice in a retriable
  PostgreSQL transaction. Only an eligible Issued or PartiallyPaid invoice whose
  due instant has passed can transition to Overdue. The invoice change, audit,
  `InvoiceBecameOverdue` outbox event and one Pending notification-delivery row
  commit atomically. Paid/Voided/not-yet-due is a safe no-op/result.

### Notification delivery and retry ownership

- The delivery row has a stable GUID and a database uniqueness constraint for
  one overdue notification per invoice workflow generation. Each invocation
  records an attempt and outcome. The workflow activity is the only retry owner:
  3 total attempts with 2-second then 4-second durable backoff. Pub/Sub does not
  retry SMTP delivery and the HTTP client has no automatic retry.
- The Mailpit message uses deterministic `Message-ID:
  <overdue-{deliveryId:N}@business-invoice-tracker.local>`. Local Compose enables
  duplicate-ID suppression. The activity checks a Delivered database row before
  sending; after an uncertain result, it queries Mailpit by Message-ID before
  sending again. This closes the accepted-before-commit window for the simulated
  provider.
- Missing/invalid recipient data is permanent and records terminal Failed without
  retry. Timeouts, connection failures and configured transient Mailpit chaos are
  retryable. Exhaustion records terminal Failed and workflow output identifies
  operator action. Immediately before a send, the activity re-reads invoice state;
  a concurrent Paid or Voided transition changes the delivery to Cancelled and
  sends nothing. No API or UI calls a notification Sent before the delivery row
  is Delivered.
- An operator-triggered retry first persists a durable, idempotent workflow
  command. Each manual retry starts a new bounded cycle of at most three attempts;
  it does not erase prior attempt evidence or create an unbounded automatic loop.

### Reconciliation and operations

- PostgreSQL is authoritative. A bounded reconciler scans uniquely ordered
  100-row invoice pages and durable command/delivery backlogs. It recreates
  missing starts, reasserts active due state and terminal state once per current
  invoice version, and exposes discrepancies. This also repairs an event that a
  stale/misconfigured publisher acknowledged on the wrong topic. It never changes
  financial state based only on workflow metadata.
- Logs/traces carry business, invoice, event, workflow and delivery IDs. Metrics
  use bounded labels for event type/outcome, never tenant or invoice IDs.
- Mailpit is a local simulated provider. Its API is test evidence and recovery
  support for this Compose topology, not a claim about real email delivery.

## Alternatives considered

- Directly schedule workflows inside the API invoice transaction: rejected
  because PostgreSQL and Dapr cannot commit atomically.
- Mark inbox processed before calling Dapr without a durable command: rejected
  because a subscriber crash could permanently lose workflow creation.
- Use only Dapr workflow state for overdue/delivery status: rejected by SE3-06;
  it is not the business source of truth or an adequate audit record.
- Rely on exactly-once Pub/Sub or SMTP: rejected; neither boundary offers that
  contract. Stable IDs, inboxes, database uniqueness and reconciliation are used.
- Retry SMTP in both HttpClient resilience and Workflow: rejected because it
  amplifies attempts and obscures the attempt count.
- Add a real email provider: rejected as unnecessary assessment scope and an
  external-secret/reliability cost. Mailpit is the required simulation.

## Consequences and failure modes

There is additional durable state: workflow commands, notification deliveries,
attempts and dead letters. It earns its cost by closing real crash windows and
making retries/reconciliation inspectable. Publication and workflow signaling
remain eventual. A Redis/Dapr outage grows visible PostgreSQL backlogs while
synchronous invoice commands remain truthful. PostgreSQL outage fails subscriber,
activity and reconciliation work closed. Mailpit outage leaves a Pending or
Failed delivery and cannot create false success.

Duplicate events, workflow commands and activities are normal. Database unique
constraints and re-reads make them harmless. If a process dies after Mailpit
acceptance but before database acknowledgement, Message-ID lookup or duplicate-ID
suppression prevents a second simulated email and allows reconciliation.

A retained older publisher image was observed to claim new event types and route
them to the legacy topic. The event was truthfully marked published but absent
from the lifecycle stream. The paired-current-image verification rule prevents
mixed routing code during replica tests, while per-version reconciliation repairs
the authoritative workflow state if such a deployment error occurs.

## Fallback or reversal

If the product-level Dapr restart/event/race tests fail within the Phase 8
timebox, use the already-approved Hangfire/PostgreSQL fallback while retaining
the EF outbox, inbox, delivery tables, stable IDs and Mailpit adapter. If Mailpit
duplicate reconciliation proves unreliable, keep delivery Failed/Unknown and do
not claim success; a stronger provider-idempotency adapter would be required.
