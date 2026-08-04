# Architecture Decision Records

ADRs are immutable decision snapshots. If a decision changes, supersede the earlier
ADR rather than rewriting history. Status values are `Proposed`, `Accepted`,
`Rejected`, `Superseded`, or `Deferred`.

## Required structure

Each ADR contains:

1. Context and requirement/risk.
2. Decision.
3. Primary evidence.
4. Alternatives considered.
5. Consequences and operational cost.
6. Failure modes.
7. Reversal/fallback path.
8. Verification references.

## Phase 0 set

| ADR | Subject | Status |
|---|---|---|
| [ADR-001](001-modular-monolith-and-automation-worker.md) | Modular monolith plus automation worker | Accepted |
| [ADR-002](002-postgresql-business-source-of-truth.md) | PostgreSQL as business source of truth | Accepted |
| [ADR-003](003-dapr-workflow-and-pubsub.md) | Dapr Workflow/Pub/Sub adoption or fallback | Accepted |
| [ADR-004](004-transactional-outbox-and-inbox.md) | EF Core transactional outbox and consumer inbox | Accepted |
| [ADR-005](005-cookie-authentication-and-antiforgery.md) | Cookie authentication and antiforgery | Accepted |
| [ADR-006](006-tenant-authorization-model.md) | Tenant authorization model | Accepted |
| [ADR-007](007-invoice-number-allocation.md) | Invoice-number allocation | Accepted |
| [ADR-008](008-money-currency-tax-rounding.md) | Money, currency, tax, and rounding | Accepted |
| [ADR-009](009-invoice-lifecycle-and-edit-policy.md) | Invoice lifecycle and edit policy | Accepted |
| [ADR-010](010-openapi-client-and-runtime-validation.md) | OpenAPI client and runtime validation generation | Accepted |
| [ADR-012](012-observability-baseline-and-optional-viewer.md) | Observability backend and Compose profile | Accepted |
| [ADR-013](013-draft-invoice-command-and-audit.md) | Draft invoice command, concurrency and audit | Accepted |
| [ADR-014](014-time-zone-and-due-instant-semantics.md) | Time-zone and due-instant semantics | Accepted |
| [ADR-015](015-issue-payment-void-command-semantics.md) | Issue, payment and void command semantics | Accepted |
| [ADR-016](016-durable-overdue-workflow-and-notification-delivery.md) | Durable overdue workflow and notification delivery | Accepted |
| [ADR-017](017-bounded-dashboard-and-invoice-discovery.md) | Bounded dashboard and invoice discovery | Accepted |
| [ADR-018](018-resilience-health-and-operator-diagnostics.md) | Resilience, truthful health and operator diagnostics | Accepted |
| [ADR-019](019-migration-deployment-and-supply-chain-gates.md) | Migration deployment and supply-chain gates | Accepted |
