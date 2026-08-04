# ADR-018: Resilience, health and operator diagnostics

- Status: Accepted
- Date: 2026-08-03
- Requirements: REQ-015, REQ-017
- Risks: R-047, R-048, R-049, R-050, R-051, R-052
- Verification: Phase 10 dependency-failure, correlation, retry and clean-copy evidence

## Context

The product already has durable outbox, inbox, workflow-command, delivery,
attempt and dead-letter state, plus optional OTLP export. Phase 10 must prove the
failure semantics instead of equating a running process with readiness or a log
statement with diagnosability. The highest risks are fake readiness, compounded
retries, blocking dependency calls, high-cardinality metrics and operator state
that exists in PostgreSQL but is difficult to locate.

## Decision

- `/health/live` remains process-only. `/health/ready` checks PostgreSQL and the
  local Dapr sidecar with fixed timeouts. Automation also checks TCP reachability
  of its configured placement and scheduler control planes; Dapr's full sidecar
  health remains healthy after those already-initialized connections are lost.
  Automation reports unavailable Mailpit
  as degraded while remaining ready because invoice truth and durable workflow
  recovery do not require immediate simulated email acceptance.
- PostgreSQL connection/command/cancellation, named Dapr clients, Mailpit API and
  SMTP operations receive explicit bounds from the resilience matrix.
- Existing retry ownership is retained: EF handles declared database transients;
  the outbox owns durable publish recovery; Dapr owns Pub/Sub redelivery; the
  command dispatcher owns Dapr workflow-operation recovery; Workflow alone owns
  notification attempts; Vue retries one safe query and no mutation.
- A cached operational snapshot periodically reads bounded PostgreSQL aggregates.
  It exports low-cardinality gauges and is exposed only on the loopback-bound
  automation operations surface. It includes outbox count/age, pending/failed
  workflow commands, dead letters and pending/failed deliveries.
- Logs and traces carry trace, message, invoice, workflow and delivery IDs. Metric
  labels are limited to fixed operation/outcome values; tenant, invoice, customer,
  message and workflow IDs are forbidden as metric labels.
- The optional Aspire profile remains a disposable local viewer. Its failure must
  not affect business readiness or the default Compose topology.

## Primary evidence

ASP.NET Core 10 documentation separates readiness from process-only liveness and
supports dependency checks plus custom JSON responses. Current Dapr documentation
defines `/v1.0/healthz` as complete sidecar/component/app-channel health and
`/v1.0/healthz/outbound` as outbound readiness before the app channel. Current
.NET documentation supports explicit `HttpClient` timeouts and cancellation,
custom `Meter` instruments, and recommends cached/fast observable callbacks with
bounded tag cardinality. Npgsql documents 15s/30s defaults; this repository uses
the narrower reviewed values in the matrix. `SendMailAsync` supports cancellation;
`SmtpClient.Timeout` alone applies only to synchronous send and is not accepted as
the asynchronous bound.

## Alternatives considered

- Always-on Prometheus/Grafana/Tempo/Loki: rejected as operationally excessive.
- Mark Mailpit failure as automation-unready: rejected because it would stop
  healthy durable workflow intake and encourage restart loops during provider
  degradation.
- Retry SMTP with HttpClient/Polly and Workflow: rejected because attempts amplify.
- Query PostgreSQL inside observable metric callbacks: rejected because a slow
  callback can block metric collection; a background snapshot owns the query.
- Use unique business/invoice IDs as metric labels: rejected for cardinality and
  data-minimization reasons; those IDs belong in traces/logs.

## Consequences and operational cost

The runtime gains small health-check and snapshot queries plus one background
monitor. Failure evidence becomes inspectable without adding a default viewer.
Readiness may correctly remove an instance while liveness keeps it alive to
recover. Degraded Mailpit can accumulate durable delivery work without a false
success. The loopback operations surface is suitable for local assessment proof;
a real deployment needs an authenticated management plane.

## Failure modes

An unavailable database makes both applications unready and prevents snapshot
refresh; the last snapshot is marked stale rather than rewritten as current.
Dapr outage makes the owning application unready while outbox/command state stays
durable. Exporter loss drops optional telemetry but JSON stdout remains. If the
monitor itself fails, it logs the failure and exposes its last successful time.

## Reversal or fallback

Remove custom metrics and retain the same bounded SQL runbook queries plus JSON
logs if the monitor affects reliability. Replace Dapr Workflow with the accepted
Hangfire/PostgreSQL fallback only if dependency/restart proof fails. No fallback
may introduce a second retry owner or weaken durable state.

## Verification

Accepted by V-1100 through V-1105 and the exact Phase 10 gate V-1199. The
restoration-safe failure matrix passed both retained and fresh-copy projects;
notification correlation and optional observer loss have separate executable
evidence.
