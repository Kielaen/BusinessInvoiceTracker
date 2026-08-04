# ADR-015: Issue, payment, void, and synchronous event semantics

- Status: Accepted
- Date: 2026-07-31
- Owners: Project maintainers
- Requirements: REQ-007, REQ-008, REQ-009, REQ-010, REQ-019
- Risks: R-013, R-016, R-017, R-024, R-034, R-035
- Verification: Phase 7 PostgreSQL, API, frontend, concurrency and E2E evidence

## Context

Phase 7 crosses three irreversible boundaries: assigning an invoice number,
accepting money, and voiding a financial snapshot. Retry identity, concurrent
behavior, authorization, audit and outbox ownership must be explicit before
these commands are exposed.

## Decision

- Accountant and Owner may issue, record payments, and void. Viewer remains
  read-only. Every lookup first proves active membership for the route business.
- Each command carries a client-generated stable GUID `requestId` and the last
  observed invoice `version`. The request ID is also the outbox message ID, so a
  committed command has a durable, globally unique replay marker.
- Issue is valid only for a Draft with an active customer and at least one line.
  Number allocation, aggregate transition, `InvoiceIssued` outbox row and audit
  row commit in one PostgreSQL transaction. A rollback does not consume the
  allocation promise; no gapless promise exists.
- Payment carries a stable `requestId`, a normalized business-unique
  `idempotencyKey`, amount, and received UTC instant. Identical replay returns
  the existing effect. Reusing either identity with different material data
  returns 409. The aggregate alone decides partial/final/overdue status and
  rejects overpayment.
- Void is valid only under ADR-009. The transition, `InvoiceVoided` outbox row
  and audit row commit together. A committed same-request replay is a no-op;
  a different request against a terminal invoice conflicts.
- Every mutation is guarded by the application GUID concurrency token. Competing
  payments may not both commit from the same balance, even when separate API
  replicas execute them.
- Outbox payloads are versioned, tenant/invoice/command scoped and contain only
  identifiers, status, money, due instant and event time. Audit details omit
  customer contact data and line descriptions.
- Phase 7 proves the database write boundary only. Dapr delivery, overdue
  workflow, Mailpit and reconciliation remain Phase 8.

## Primary evidence

ADR-007 records PostgreSQL's atomic `ON CONFLICT ... RETURNING` allocation
evidence. ADR-004 governs the transactional outbox. ADR-009 and EF concurrency
evidence govern state transitions and stale writers. Phase 7 must still execute
simultaneous issue and payment attempts against PostgreSQL; documentation alone
does not establish the gate.

## Alternatives considered

- A generic command-receipt table: deferred because each required mutation
  already writes a stable outbox record, and Payment persists both command and
  business idempotency identities. Add a receipt table only for a command that
  legitimately produces no durable event.
- Database locks without a version token: rejected because callers would not
  receive an explicit stale-state signal and long lock waits add no product value.
- Direct Dapr publish in the request: rejected because it can diverge from the
  invoice transaction.
- Allowing a payment to void/reopen implicitly: rejected; refunds, reversals and
  credit notes require separate accounting semantics outside this assessment.

## Consequences and failure modes

A transaction failure leaves invoice, sequence, payment, audit and outbox
unchanged. An uncertain response can be retried with the same request identity.
Concurrent writers produce one commit and bounded 409 losers. Publisher retries
may duplicate delivery, so Phase 8 consumers must retain inbox idempotency.
PostgreSQL unavailability fails closed; no in-memory replay state exists.

## Fallback or reversal

If outbox identity cannot represent a later command's replay data, add a small
command-receipt table with request hash and result identity in a new ADR. If
regulatory payment reversal is required, add explicit reversal/credit-note
states and events rather than weakening Paid or Voided terminal rules.
