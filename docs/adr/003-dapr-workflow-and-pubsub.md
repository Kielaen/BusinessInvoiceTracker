# ADR-003: Dapr Workflow and Pub/Sub adoption gate

- Status: Accepted
- Date: 2026-07-30
- Requirements: REQ-010, REQ-011, REQ-020
- Risks: R-003 through R-006, R-016
- Verification: V-0100 through V-0103

## Context

Overdue work must survive restart, accept payment/void/due-date events, tolerate
duplicates, and avoid an in-process timer claim. Dapr adds scheduler, placement,
state-store and sidecar operations, so documentation and compilation alone are
insufficient.

## Decision

Adopt Dapr runtime 1.17.7 and Dapr .NET SDK/Workflow 1.17.9 on .NET 10.0.
Workflow actor state uses Dapr's stable PostgreSQL v2 component in dedicated
coordination tables. Redis 7.4.10 is retained only for Pub/Sub. Scheduler and
Placement are explicit pinned services. Workflow activities re-read PostgreSQL
business truth and remain idempotent.

Dapr owns durable workflow retry. Application handlers may retry only a distinct
boundary with bounded attempts; Compose and nginx do not add write retries.

## Primary evidence

The pinned releases and compatibility policy are linked in
`docs/evidence/primary-sources.md`. Dapr documents persisted workflow history,
durable actor reminders, and buffering of events for an existing workflow.
The repository compiles the worker with no warnings or vulnerable packages.
V-0101-R1 and V-0102-R1 prove buffered external events and a durable timer after
the worker and sidecar are both stopped and restarted. V-0103-R1 repeats those
checks from a clean copy.

## Alternatives considered

- Hangfire with PostgreSQL storage: established fallback with fewer control-plane
  services.
- Quartz.NET persistent store: viable scheduling but more custom orchestration.
- HostedService/in-memory timers: rejected because restart durability fails.

## Consequences and failure modes

The decision adds Redis Pub/Sub, a sidecar, Scheduler, Placement, and separate
PostgreSQL workflow tables. Duplicate delivery and workflow replay are normal;
business effects must be guarded by PostgreSQL idempotency/inbox records. Events
for nonexistent/completed workflows may be discarded, so the outbox publisher
must create/coordinate instances deliberately. Coordination-table, Redis, or
Scheduler loss delays automation; business tables remain authoritative.

## Reversal or fallback path

If later product-level idempotency, retry, multi-replica, or recovery tests fail,
select Hangfire with PostgreSQL-backed jobs, remove Dapr/Redis/Scheduler/Placement,
and supersede this ADR before claiming the affected gate.

## Verification

Build, package scan, optional-profile, restart/event, default-topology, and
clean-copy checks pass. The Redis actor-state candidate was rejected after Dapr
warned it lacks rollback semantics; the same runtime gate passes with PostgreSQL
v2 actor state. See V-0100, V-0101-R1, V-0102-R1, and V-0103-R1.
