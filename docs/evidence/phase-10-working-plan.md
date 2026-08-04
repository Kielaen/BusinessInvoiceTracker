# Phase 10 Working Plan

Status: **PASSED (V-1199)**  
Started: 2026-08-03 (Africa/Johannesburg)  
Previous gate: Phase 9 passed as V-1099-R1.

## Objective

Prove truthful, bounded and diagnosable behaviour when each required dependency
or process is unavailable, without adding an always-on observability dependency.

## Governing rules

The constitution and implementation plan remain binding. PostgreSQL remains the
business source of truth. Liveness is process-only; readiness names required
dependencies. Retrying layers must own different boundaries, carry fixed attempt
or cadence bounds, and preserve idempotency. Metrics use bounded labels; logs and
traces may carry safe business IDs but no credentials or customer PII.

## Work sequence

Only Phase 10 is active:

1. Record primary-source evidence, material risks, ADR-018 and the final
   resilience matrix before changing runtime policy.
2. Bound PostgreSQL, Dapr, Mailpit and health-probe operations with explicit
   timeouts while preserving the existing single retry owner per boundary.
3. Make API and automation readiness truthful, keep liveness process-only, and
   represent Mailpit as an optional degraded dependency.
4. Add bounded-cardinality domain telemetry and a cached operator snapshot for
   outbox, workflow-command, dead-letter and notification backlogs.
5. Add operational runbooks and executable failure probes for PostgreSQL, API
   Dapr, automation Dapr, Redis/Pub/Sub, scheduler, placement, Mailpit, the web/API
   boundary and the optional telemetry viewer.
6. Prove one notification failure can be followed by trace/workflow/delivery ID,
   prove attempt counts do not amplify, run all regressions and the clean-copy
   gate, then reconcile evidence.

## Gate

- Every dependency failure has tested expected behaviour.
- No retry amplification.
- A notification failure is diagnosable from one trace/workflow ID.
- Outbox backlog and failed workflow/delivery state are operator-visible.

## Scope guard

Phase 10 does not add Prometheus, Grafana, Loki, Tempo, a message broker beyond
the adopted Dapr/Redis topology, or an always-on telemetry backend. The existing
optional Aspire profile remains local, unauthenticated and in-memory.

## Completion

- PostgreSQL, HTTP, SMTP, workflow-command and control-plane bounds are
  implemented without overlapping retry ownership.
- Process-only liveness and dependency-aware readiness passed controlled stops
  for API, automation, web, PostgreSQL, Redis, both Dapr sidecars, placement,
  scheduler, Mailpit and the optional observer.
- Cached low-cardinality metrics plus the loopback operator endpoint expose
  outbox age/count, workflow command state, dead letters and notification state.
- Notification failure correlation passed from trace ID to workflow/delivery ID
  and durable attempts.
- The full locked regression and fresh-copy/fresh-volume gate pass as V-1101 and
  V-1105. Phase 10 gate is V-1199.
- The operator endpoint remains a loopback-only local assessment surface, not an
  authenticated deployment management plane. R-053 also records that retained
  `InvoiceBecameOverdue` events make the generic outbox aggregate ambiguous
  until event types are inspected. Neither limitation weakens the Phase 10
  visibility proof, and neither is represented as production completion.

Next phase: Phase 11, migrations, supply-chain controls and CI hardening.
