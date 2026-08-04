# Submission Architecture and Trade-offs

## System shape

```text
Browser :8080
  -> unprivileged nginx (Vue SPA + same-origin /api and /health proxy)
     -> ASP.NET Core API
        -> PostgreSQL (identity, tenant/domain truth, audit, outbox)
        -> local Dapr sidecar -> Redis Pub/Sub
                                  -> automation sidecar/host
                                     -> PostgreSQL + Dapr Workflow state
                                     -> Mailpit SMTP simulation

Optional: both .NET hosts -> OTLP -> local Aspire dashboard
```

The API is a modular monolith for synchronous product behavior; automation is a
separate host because durable timing, at-least-once delivery and simulated email
have different failure ownership. PostgreSQL is the business source of truth.
Dapr owns Pub/Sub and durable workflow behavior only after its pinned timer,
external-event and restart semantics were proven. See
[ADR-001](../adr/001-modular-monolith-and-automation-worker.md),
[ADR-002](../adr/002-postgresql-business-source-of-truth.md) and
[ADR-003](../adr/003-dapr-workflow-and-pubsub.md).

## Correctness boundaries

- Tenant/resource authorization rereads an active membership and filters every
  resource query by `BusinessId`; inaccessible foreign IDs return 404.
- Browser authentication uses ASP.NET Core Identity cookies and antiforgery
  behind nginx. Non-development mode requires secure `__Host-` cookies.
- Draft totals are server-calculated. Invoice lifecycle rules live in the domain,
  and PostgreSQL constraints support tenant/customer, numbering and uniqueness
  invariants.
- Optimistic versions reject stale updates. Stable command/request/payment IDs,
  unique constraints and transactions make exact replay safe and changed-payload
  reuse a conflict.
- Invoice issue/payment/void commits domain state, audit and outbox atomically.
  Dapr delivery is at least once; automation inbox/commands and notification rows
  make business effects idempotent and recoverable.

The relevant decisions are [ADR-004](../adr/004-transactional-outbox-and-inbox.md),
[ADR-006](../adr/006-tenant-authorization-model.md),
[ADR-007](../adr/007-invoice-number-allocation.md),
[ADR-008](../adr/008-money-currency-tax-rounding.md),
[ADR-009](../adr/009-invoice-lifecycle-and-edit-policy.md) and
[ADR-016](../adr/016-durable-overdue-workflow-and-notification-delivery.md).

## Failure and retry ownership

Each boundary has one retry owner. API-to-Dapr publication uses durable outbox
leases and one HTTP attempt per lease. Dapr owns inbound Pub/Sub delivery.
Workflow-command submission has its own durable bounded dispatcher. Dapr Workflow
owns notification activity retries; SMTP performs no nested retry. Browser reads
may retry once, while mutations never retry automatically because their outcome
may be unknown. Exact bounds and terminal states are in the
[resilience matrix](../evidence/resilience-matrix.md).

Readiness checks PostgreSQL and the required local sidecar; automation also checks
placement and scheduler. Mailpit loss is degraded rather than unready because
durable intake can continue. Liveness is process-only. Operational snapshots
separate publishable outbox backlog from intentionally retained domain-event
history, expose failure inventories and mark stale database snapshots explicitly.

## Contract and frontend

ASP.NET build-time OpenAPI is the sole client contract source. Exact-pinned Hey
API generation produces TypeScript and Zod schemas; CI fails on generated drift.
Every success response consumed by Vue is runtime-validated. Vue Query owns
server state and cancellation, Pinia stores only the selected workspace, routes
are lazy, mutations invalidate explicit keys, and root/feature error containment
keeps malformed or failed responses visible. All API collections are paginated
or deliberately fixed-limit; filters/sorts run in PostgreSQL.

## Operational topology

The root Dockerfile builds three final non-root targets: API, automation and web.
Compose adds PostgreSQL, Redis, Mailpit and the Dapr control plane. A one-shot
migrator must complete before application hosts start. Images and critical tools
are exact-tag/digest pinned; CI uses full-SHA actions, read-only repository
permissions, locked dependency restores, generated/model drift checks, tests,
image scans, SBOMs and a clean Compose reviewer journey. Optional telemetry does
not participate in business readiness.

## Why the complexity is justified

| Choice | Risk it earns its cost against | Simpler alternative / fallback |
|---|---|---|
| Modular API + automation host | Durable timers and notification failures should not live inside request handling | One process if overdue simulation becomes synchronous; rejected for restart durability |
| Dapr Workflow/Pub/Sub | Required durable due timer, external events, restart and duplicate behavior | Hangfire fallback was defined before adoption; Dapr retained only after executable spikes |
| Transactional outbox/inbox | Database commit cannot atomically include broker delivery | Direct publish rejected because a crash can lose lifecycle work |
| Cookie + antiforgery | Same-origin browser app does not need bearer-token storage | Standards-based OIDC is the deployment fallback when external identity is required |
| Generated SDK + Zod | Compile-time TypeScript alone cannot validate network data | Handwritten types rejected; Kiota plus explicit Zod was the fallback |
| Optional Aspire | Useful local trace/metric inspection without availability coupling | JSON stdout and repository queries remain the default |
| One-shot migrations | Replica startup migration races and excessive app privileges | Deployment job/immutable bundle for a real platform; runtime auto-migrate rejected |

## Evidence and non-claims

The [requirements traceability](../evidence/requirements-traceability.md) maps the
brief to proof, the [verification ledger](../evidence/verification.md) preserves
both failures and passes, and [known limitations](../submission/known-limitations.md)
defines what this assessment does not claim. Local diagnostic performance is not
a production SLO; Compose is not represented as a cloud production deployment.
