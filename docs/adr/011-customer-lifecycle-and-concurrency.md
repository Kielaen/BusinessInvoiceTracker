# ADR-011: Customer lifecycle, duplicate create, and concurrency

- Status: Accepted
- Date: 2026-07-31
- Owners: Project maintainers
- Requirements: REQ-003, REQ-004, REQ-015, REQ-018, REQ-019
- Risks: R-021, R-025, R-026, R-028
- Verification: Phase 5 domain, PostgreSQL, API, frontend and E2E evidence

## Context

Customer records are tenant-owned inputs to invoices. The first product slice
must decide validation, duplicate submission, stale edits, search, pagination,
and deletion semantics before exposing mutable endpoints. A valid customer ID
must never be sufficient to cross the tenant boundary.

## Decision

- A customer belongs permanently to one business. Every query and mutation uses
  both `BusinessId` and `CustomerId` after loading the caller's current active
  membership from PostgreSQL.
- Viewer may read. Accountant and Owner may create and edit.
- Name, normalized email, billing address line 1, city, postal code, and
  two-letter country code are required. Phone and address line 2 are optional.
  Length and format bounds exist in the domain, API, and database schema.
- Email is unique per business, not globally.
- Create carries a client-generated request/resource ID. Repeating the same ID
  with the same normalized representation returns the existing customer;
  reusing it with different data returns 409. The primary key and unique tenant
  email index close concurrent races.
- Updates carry the last observed GUID version. A mismatched or concurrently
  replaced version returns 409; every successful material change creates a new
  version.
- Phase 5 exposes no physical delete. Existing inactive-state support is kept
  for a future explicit archive policy. PostgreSQL restrictive invoice foreign
  keys ensure a referenced customer cannot be physically removed accidentally.
- List queries use bounded offset pagination for this assessment, a fully unique
  `(Name, Id)` order, and escaped PostgreSQL `ILIKE` search over name and email.
  The response includes page, page size, total count, and total pages.

## Primary evidence

EF Core documents that pagination ordering must be fully unique. Npgsql
documents translation of `EF.Functions.ILike`, including an explicit escape
character. ASP.NET Core provides `WebApplicationFactory` for functional tests,
and TanStack Query provides an `AbortSignal` that cancels an obsolete request
when consumed by the query function. Sources are recorded in the evidence
ledger.

## Alternatives considered

- Global customer email uniqueness: rejected because the same customer may buy
  from unrelated businesses.
- Trusting a business ID in the request body: rejected; tenancy comes from the
  authorized route and membership.
- Last-write-wins edits: rejected because concurrent contact/billing changes can
  be silently lost.
- Immediate full-text search infrastructure: rejected; bounded `ILIKE` is enough
  for assessment-scale data and has a simpler operational profile.
- Hard delete or cascade delete: rejected because invoices and audit history
  must retain their customer reference.
- A generic idempotency framework in Phase 5: rejected until a second command
  requires it. A stable create ID plus database constraints solves this command's
  concrete duplicate risk without premature infrastructure.

## Consequences

The client must retain versions and surface conflicts instead of silently
overwriting. Offset pagination can shift under concurrent inserts, but it never
returns an unbounded result and its unique order is deterministic for a query
snapshot. Search remains database-executed and case-insensitive.

## Failure modes

- Missing membership and cross-tenant IDs return 404; an insufficient same-
  tenant role returns 403.
- Duplicate tenant email, reused create ID with different data, and stale edit
  return 409 Problem Details.
- Duplicate identical create is safe before and after restart and across
  replicas because identity is stored in PostgreSQL.
- Cancellation stops obsolete browser fetches and propagates to the API request
  cancellation token and EF query.
- Search wildcard characters are escaped and treated as user text.

## Reversal or fallback path

Move to keyset pagination only when dataset size or page-depth evidence justifies
the UX trade-off. Add a generic idempotency table when a second materially
important command needs stored replay semantics. Archival, restoration, data
retention, and anonymisation require a separate policy and tests before exposing
customer removal.

