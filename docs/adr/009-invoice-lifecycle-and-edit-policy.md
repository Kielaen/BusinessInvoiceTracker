# ADR-009: Invoice lifecycle and edit policy

- Status: Accepted
- Date: 2026-07-31
- Owners: Project maintainers
- Requirements: REQ-006, REQ-008, REQ-009, REQ-010
- Risks: R-014, R-017
- Verification: V-0403, V-0405; Phase 7/8 command and workflow race tests

## Context

The candidate lifecycle leaves editing, voiding paid invoices, partial payment
after the due boundary, and stale updates ambiguous. These answers must be stable
before the aggregate and database schema encode them.

## Decision

- Draft invoices may change customer, dates, and lines. They have no payments.
- Issuance requires an active customer reference, at least one valid line, an
  issue date, and a due date on or after the issue date.
- Issuance snapshots number, lines, currency, totals, due date, and the UTC due
  instant. Issued invoices are immutable; corrections use void-and-replace.
- Draft, Issued, and unpaid Overdue invoices may be voided only when paid amount
  is zero. An invoice with any payment cannot be voided in this assessment.
- Positive payments may be recorded only after issue and may not exceed the
  outstanding amount.
- Issued becomes PartiallyPaid after a non-final payment and Paid after the final
  payment.
- Issued or PartiallyPaid becomes Overdue only at or after the persisted due
  instant while outstanding is positive.
- A partial payment on an Overdue invoice remains Overdue; a final payment becomes
  Paid.
- Voided and Paid are terminal.
- Every material mutation changes an application-managed GUID concurrency token.

The due instant follows ADR-014: first instant of the day after the displayed due
date in `Africa/Johannesburg`, persisted in UTC.

## Primary evidence

EF Core documents application-managed concurrency tokens and
`DbUpdateConcurrencyException` when the original token no longer matches. The
source is recorded in `docs/evidence/primary-sources.md`. Lifecycle rules are
explicit product decisions, not inferred framework behavior.

## Alternatives considered

- Editing issued invoices: rejected because it silently rewrites a financial
  snapshot and complicates audit/workflow reconciliation.
- Voiding after payment: rejected because refund/reversal accounting is not in
  scope.
- Clearing Overdue after a partial late payment: rejected because an outstanding
  balance remains late.
- Database trigger state machine: rejected because domain methods must remain the
  understandable primary rule boundary; database constraints still defend
  representable scalar invariants.

## Consequences

The state machine is small and explainable. Corrections after issue create a
visible void-and-replace trail. Refunds, credit notes, payment reversal,
write-offs, and reopening are excluded.

## Failure modes

Invalid and duplicate transitions throw coded domain violations without partial
mutation. Competing writers use the original GUID version in the SQL predicate;
one succeeds and the loser receives a concurrency exception. Restart and replica
count do not change state rules. Phase 7 must add idempotency around payment
commands; Phase 8 must re-read state/version before overdue mutation so stale
timers are harmless.

## Reversal or fallback path

Adding correction, refund, or credit-note workflows requires new explicit states,
events, accounting rules, and migration tests. Do not reuse void as an implicit
refund.

## Verification

Phase 3 exhaustively tests valid and invalid transitions plus a genuine
PostgreSQL stale-writer conflict. Phase 7 and Phase 8 retain command-idempotency,
payment race, and workflow race proof.
