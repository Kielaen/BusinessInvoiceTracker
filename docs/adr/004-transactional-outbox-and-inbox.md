# ADR-004: EF Core transactional outbox and consumer inbox

- Status: Accepted
- Date: 2026-07-30
- Requirements: REQ-010, REQ-011, REQ-019
- Risks: R-016, R-017
- Verification: Phase 8 crash-window, duplicate and concurrency tests

## Context

An invoice transaction and a broker publish cannot commit atomically. A crash
between them can lose work; a crash after publish but before acknowledgement can
duplicate it.

## Decision

Write domain changes, audit entries and outbox rows in one EF Core/PostgreSQL
transaction. A bounded dispatcher claims pending rows, publishes them, and records
success. Consumers first insert a unique inbox/message-processing record in the
same transaction as business effects. Message IDs and operation idempotency keys
are stable across retries. The publisher owns publish retry; consumers own only
their local transaction retry.

## Primary evidence

This is derived from the required failure windows in the constitution and plan.
It is an architecture contract, not a Phase 0 claim of implementation. ADR-002
keeps the atomic boundary in PostgreSQL and ADR-003 treats Dapr delivery as
at-least-once.

## Alternatives considered

- Direct publish after commit: can lose events.
- Publish before commit: can expose rolled-back state.
- Distributed transaction: not supported by the selected topology and adds
  unjustified coordination.
- Broker-only deduplication: cannot protect database side effects.

## Consequences and failure modes

The crash-after-publish window deliberately creates a duplicate; the inbox makes
it harmless. Concurrent dispatchers require row claiming/locking. Poison messages
need bounded attempts and diagnosable terminal state. Backlog metrics and oldest
pending age are required.

## Reversal or fallback path

If the final broker changes, keep the database outbox/inbox contracts. Removing
them requires proof of an equally atomic delivery boundary.

## Phase 1 implementation evidence

The walking skeleton atomically writes a probe and outbox row. The publisher uses
a capped exponential retry schedule and Dapr CloudEvents. The worker executes its
unique inbox insert and receipt in one EF execution-strategy transaction. V-0203
replayed the same message twice and observed two successful acknowledgements,
two duplicate logs, and no second effect.

This proves the basic duplicate boundary, not the crash-after-publish or
multi-dispatcher claim. Phase 8 must still kill the dispatcher in each crash
window and test concurrent publishers before those stronger claims are made.
