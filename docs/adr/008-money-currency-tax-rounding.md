# ADR-008: Money, currency, tax, and rounding

- Status: Accepted
- Date: 2026-07-31
- Owners: Project maintainers
- Requirements: REQ-006, REQ-015
- Risks: R-015
- Verification: V-0402

## Context

The tracker must calculate authoritative invoice totals without implying legal,
tax-filing, or general accounting correctness. Decimal scale, midpoint rounding,
line aggregation, currency mixing, negative values, and tax precision must be
explicit before code exists.

## Decision

Use .NET `decimal` and PostgreSQL `numeric`, never binary floating point.

- Currency is a three-letter uppercase code. Phase 3 tests and demo data use ZAR.
- Stored money has two decimal places and cannot be negative.
- Quantity is positive with at most four decimal places.
- Tax rate is between 0 and 100 inclusive with at most four decimal places.
- Round using `MidpointRounding.AwayFromZero` to two decimals.
- For each line, round `quantity × unit price` to obtain line net, then round
  `line net × tax rate / 100` to obtain line tax.
- Invoice subtotal is the sum of rounded line nets; invoice tax is the sum of
  rounded line taxes; total is subtotal plus tax.
- All lines, payments, and totals on one invoice use the invoice currency.

This is a fixed demonstration tax model, not jurisdictional advice or tax
compliance.

## Primary evidence

.NET documents explicit midpoint strategies for `Decimal.Round`. The framework
behavior is recorded in `docs/evidence/primary-sources.md`; the choice of
AwayFromZero and per-line tax rounding is a product rule. Boundary fixtures prove
the repository's implementation.

## Alternatives considered

- `double`: rejected because binary representation is inappropriate for exact
  stored monetary values.
- Banker's/ToEven rounding: valid in other domains but less intuitive for the
  deliberately simple reviewer model.
- Round tax only at invoice total: rejected because line presentation would not
  reconcile deterministically with displayed totals.
- Multi-currency conversion: rejected because exchange rates, rate dates, and
  gains/losses are outside the assessment.

## Consequences

Displayed lines sum exactly to displayed totals. A high-precision source unit
price is not supported; accepting one requires a new pricing rule. Credits,
discounts, inclusive tax, compound tax, exemptions, and currency conversion are
not implemented.

## Failure modes

Negative money, zero/negative quantity, excessive scale, invalid currency, and
out-of-range tax fail before persistence. Repeated calculation is pure and
deterministic. Concurrent replicas obtain the same result because culture and
host rounding defaults are not consulted. Database check constraints provide a
second defense.

## Reversal or fallback path

New tax or pricing models require versioned calculation policies and migration
rules for existing drafts. Issued invoice amounts remain immutable snapshots.

## Verification

Phase 3 unit tests cover zero tax, normal ZAR tax, midpoint rounding, multiple
lines, fractional quantity, scale/range rejection, currency mismatch, and
overflow bounds.
