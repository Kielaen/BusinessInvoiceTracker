# ADR-014: Time-zone and due-instant semantics

- Status: Accepted
- Date: 2026-07-30
- Requirements: REQ-006, REQ-010
- Risks: R-014
- Verification: Later clock-controlled domain/workflow tests

## Context

A date-only due date has no unique instant. Treating it as UTC midnight can mark
an invoice overdue during the customer's stated due day, and host-local time makes
replicas disagree.

## Decision

The assessment uses one documented business time zone:
`Africa/Johannesburg`. An invoice is due throughout its displayed due date and
becomes overdue at the first instant of the next local calendar day. Convert that
boundary to UTC when scheduling; store both the date-only business due date and
the derived UTC due instant. All persisted event timestamps are UTC.

If the due date changes, increment the invoice concurrency/version token and
signal the durable workflow. The workflow re-reads PostgreSQL; a stale timer cannot
mark an invoice paid, voided, already overdue, or associated with a newer due
version. Partial payment after the boundary leaves the invoice overdue while the
outstanding amount is positive.

## Primary evidence

This is an explicit product rule resolving an ambiguity in the assessment and
governing plan; it is not inferred from a library default. The IANA identifier is
portable across Linux containers. Johannesburg currently has no daylight-saving
transition, but code must use a time-zone database rather than a fixed offset.

## Alternatives considered

- Start of the displayed due date: rejected because it removes most of that day.
- UTC date semantics: rejected because users operate in a stated business zone.
- Per-tenant time zones: useful future scope but requires tenancy settings,
  migration and rescheduling semantics not demanded by the assessment.

## Consequences and failure modes

Every replica derives the same instant. Time-zone rule updates can change future
derivations, so the scheduled UTC instant is persisted and audited. Duplicate or
stale timer/event execution is harmless only when the database version/state check
is atomic. Operators can compare business date, zone, derived instant and version.

## Reversal or fallback path

Per-tenant time zones require a migration ADR, explicit effective-date rules, and
rescheduling tests. Until then the single zone is a truthful limitation.

## Verification

Phase 3 tests must cover the due day boundary with a controlled clock. Phase 8
must cover due-date change, payment/void races, stale timers and restart recovery.
