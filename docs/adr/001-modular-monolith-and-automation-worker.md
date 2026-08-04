# ADR-001: Modular monolith plus automation worker

- Status: Accepted
- Date: 2026-07-30
- Requirements: REQ-001, REQ-010, REQ-013, REQ-015
- Risks: R-001, R-003, R-006
- Verification: V-0103, V-0199

## Context

The assessment requires end-to-end invoice behaviour and durable overdue
automation. Service boundaries would add deployment, consistency, security and
diagnostic costs without an independent scaling or ownership requirement.

## Decision

Use one ASP.NET Core modular monolith for HTTP and business transactions, one Vue
SPA served by nginx, and one separately runnable automation worker. PostgreSQL is
the business database. The worker may use Dapr only if ADR-003's runtime gate
passes. Modules communicate in-process or through durable messages, not direct
cross-module table writes.

## Primary evidence

The assessment and governing plan require a full-stack tracker, not microservices.
The Phase 0 Compose file contains only the proposed application and worker
boundaries; supporting containers are infrastructure, not application services.

## Alternatives considered

- Multiple domain microservices: rejected because distributed transactions and
  additional deployments do not satisfy a demonstrated requirement.
- A single process including timer work: simpler to deploy, but couples HTTP
  availability and durable automation lifecycle.
- Serverless functions: rejected because they add a second hosting model.

## Consequences and failure modes

Transactions remain local to PostgreSQL. Automation can restart or scale
independently, but duplicate execution is expected and must be idempotent.
Multiple API replicas share keys and database constraints. If the worker is down,
HTTP remains available and durable work waits. Operators get correlated JSON/OTLP
telemetry from two application processes.

## Reversal or fallback path

The worker can be hosted in the API process only if deployment constraints demand
it and restart/durability tests still pass. New services require a later ADR with
a measured boundary or scaling need.

## Verification

V-0103-R1 proves the Phase 0 topology from a clean copy, and V-0199-R1 proves the
Phase 0 architecture gate. Product walking-skeleton evidence remains Phase 1.
