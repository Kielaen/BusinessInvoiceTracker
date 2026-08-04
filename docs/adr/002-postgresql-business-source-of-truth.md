# ADR-002: PostgreSQL as business source of truth

- Status: Accepted
- Date: 2026-07-30
- Requirements: REQ-006, REQ-008, REQ-009, REQ-019
- Risks: R-013, R-017
- Verification: V-0103 and later PostgreSQL integration/concurrency tests

## Context

Invoices, payments, tenant membership, lifecycle transitions, idempotency records
and outbox rows require relational constraints and atomic transactions.

## Decision

PostgreSQL business tables are authoritative for all business state. Separate Dapr
PostgreSQL v2 workflow tables are coordination state only and can never determine
invoice truth; Redis is Pub/Sub only. Amounts use exact decimal database types;
IDs and tenant IDs participate in constraints and query filters; concurrency uses
explicit tokens and database-enforced uniqueness.

## Primary evidence

The governing plan's domain and consistency sections require PostgreSQL-backed
integration and concurrency evidence. The Phase 0 topology pins PostgreSQL
17.10-alpine3.23 by digest.

## Alternatives considered

- Redis as business state: rejected because workflow convenience must not split
  authority.
- Document database: rejected because the required relational constraints and
  transaction boundaries gain no demonstrated benefit.
- In-memory persistence: permitted only in isolated unit tests, never integration
  evidence.

## Consequences and failure modes

Database availability gates business writes. Concurrent commands are resolved by
constraints/tokens rather than last-write-wins. Retries must preserve idempotency
keys. Backups and migrations become operational requirements. Loss of Dapr
coordination tables, Scheduler, or Redis Pub/Sub may delay automation but must not
corrupt invoice state.

## Reversal or fallback path

Changing the business store requires a migration ADR and equivalent atomicity,
constraint, concurrency, tenant-isolation and recovery evidence.

## Verification

Phase 0 proves only the pinned container can join the topology (V-0103). Schema,
migration and real-concurrency evidence belongs to Phases 1, 3 and 7.
