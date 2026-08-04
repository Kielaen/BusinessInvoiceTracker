# ADR-007: Invoice-number allocation

- Status: Accepted
- Date: 2026-07-31
- Owners: Project maintainers
- Requirements: REQ-007, REQ-019
- Risks: R-013
- Verification: V-0404

## Context

Invoice numbers must be unique within a business and allocated atomically.
Calling them “gapless” would create an accounting promise the assessment neither
requires nor provides operational rules to maintain across voids, failed work,
restores, or manual intervention.

## Decision

Maintain one `InvoiceNumberSequence` row per business. Allocate with one
PostgreSQL `INSERT ... ON CONFLICT DO UPDATE ... RETURNING` statement. The first
value is 1 and every committed allocation advances by one. Format the immutable
display value as `INV-` followed by an eight-digit sequence.

The promise is unique and monotonically increasing allocation per business.
Gaplessness is explicitly not promised. A unique `(BusinessId, InvoiceNumber)`
constraint is the final defense.

## Primary evidence

PostgreSQL documents that `ON CONFLICT DO UPDATE` guarantees an atomic insert-or-
update outcome under high concurrency and that `RETURNING` exposes the updated
value. The source is recorded in `docs/evidence/primary-sources.md`. The actual
claim still requires concurrent execution against PostgreSQL.

## Alternatives considered

- PostgreSQL global sequence: unique but mixes tenants and cannot express an
  independently increasing business sequence.
- `MAX(number) + 1`: rejected because concurrent readers can allocate the same
  value.
- Gapless ledger numbering: rejected because rollback, deletion, restore, and
  legal policy semantics are out of scope and would require stronger operational
  controls.
- Yearly reset: rejected because boundary and time-zone concurrency add no
  required product value.

## Consequences

Allocation serializes briefly on one row per business while unrelated businesses
remain independent. Numbers are not reused. ADR-013 amends the original Phase 6
wording: allocation occurs atomically in the Phase 7 issue transaction because a
draft has no number and issuance is the immutable snapshot boundary.

## Failure modes

Duplicate or concurrent calls receive distinct values. A rolled-back transaction
may release its increment, but no caller may rely on reuse or gaplessness. A
retry after an uncertain outcome must be owned by the idempotent invoice-creation
command; the allocator alone cannot determine whether the caller already used a
number. Database unavailability fails the allocation. Replica count does not
change correctness because PostgreSQL arbitrates the row.

## Reversal or fallback path

If a future regulatory requirement demands a fiscal sequence, add a separate
posting ledger and migration ADR. Do not silently strengthen this display-number
promise.

## Verification

V-0404 runs 40 simultaneous allocations through independent DbContexts and
asserts uniqueness, the exact 1-40 range, and per-business independence.
