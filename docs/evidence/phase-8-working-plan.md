# Phase 8 Working Plan

Status: **PASSED**  
Started: 2026-07-31 (Africa/Johannesburg)  
Completed: 2026-08-01 (Africa/Johannesburg)  
Previous gate: Phase 7 passed as V-0899.

## Objective

Implement durable overdue evaluation and simulated notification without
weakening the already-proven synchronous invoice lifecycle.

## Governing rules

All constitution rules remain binding. The phase protects complete delivery,
tenant isolation, financial state, idempotency, restart recovery, bounded retry,
operability and truthful claims ahead of optional infrastructure. ADR-003,
ADR-004, ADR-005, ADR-014 and ADR-015 are the current decision baseline.

## Initial risks and evidence questions

- Lifecycle outbox publication must be retryable without duplicate business
  effect, and inbox ownership must be explicit at the automation boundary.
- Workflow creation, event-before-wait, paid/void/due-date races, timer restart,
  worker/sidecar replacement and reconciliation must be exercised, not inferred
  from the Phase 0 spike.
- Overdue eligibility must re-read PostgreSQL at the activity boundary using the
  Africa/Johannesburg due policy and remain safe after concurrent payment.
- Mailpit delivery records and retries need one owner, finite attempts, stable
  identity, diagnosable failure and no duplicate successful result.
- Phase 7 pending lifecycle rows must not be marked published until Dapr accepts
  them, and duplicate Pub/Sub delivery must be absorbed by the inbox.

## Work sequence

Only Phase 8 is active:

1. Re-read the governing documents and current ledger; map every required
   workflow proof and update risks before implementation.
2. Verify current Dapr Workflow, Pub/Sub and Mailpit facts from primary sources;
   write the Phase 8 ADR for orchestration identity, retry ownership,
   cancellation/events, reconciliation and notification identity.
3. Extend the outbox publisher for lifecycle events and implement the automation
   inbox/subscription boundary with stable deduplication.
4. Implement the smallest durable issued-invoice workflow, database-rechecking
   overdue activity, simulated notification, attempt/outcome persistence and
   external paid/void/due-date handling.
5. Add reconciliation and focused failure injection for lost wakeups, duplicate
   delivery, timer/worker/sidecar restart, concurrent payment and bounded retry.
6. Run prior regressions, generated drift, vulnerability, observability, Docker
   and clean-copy gates, then update all evidence before a gate decision.

## Completed result

- ADR-016 records workflow identity, retry ownership, cancellation, delivery
  identity, reconciliation, failure modes and the approved fallback.
- Lifecycle outbox leases, Pub/Sub, transactional inbox/dead-letter handling and
  durable workflow commands pass V-0900 through V-0905.
- Durable timer restart and due-date rescheduling pass V-0906. Paid-before-wait,
  paid/timer and void/timer races terminate or cancel without notification as
  proven by V-0908.
- Mailpit delivery uses stable identity, one retry owner and durable attempts.
  Transient recovery across worker replacement, permanent terminal failure and
  durable manual retry pass V-0907.
- PostgreSQL-authoritative workflow and accepted-before-ack delivery repair pass
  V-0909. The reconciler reasserts current active/terminal state once per invoice
  version and scans bounded 100-row pages.
- Two current API publishers split 100 claims while every row retained exactly
  one publish attempt and side effect (V-0910). A stale replica routing failure
  was detected, recorded and fixed by paired image recreation plus typed logging.
- Full regressions and a fresh-directory/fresh-volume Compose journey pass
  V-0911 and V-0912. The Phase 8 gate decision is V-0913.

## Gate

- Every workflow proof required by plan section 9.3 passes.
- Duplicate delivery is safe.
- Workflow and business state can be reconciled.
- No user-visible success is reported before the relevant durable guarantee
  exists.

Gate result: **PASS**. Every listed condition has executable repository evidence.

## Scope guard

Phase 8 does not add dashboard aggregation or broader invoice query UX (Phase
9), chaos/load hardening (Phase 10), or final delivery polish (Phases 11-12).
Phase 9 is the exact next phase and has not been started by this plan.
