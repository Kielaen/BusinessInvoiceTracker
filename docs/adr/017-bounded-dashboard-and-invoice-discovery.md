# ADR-017: Bounded dashboard and invoice discovery queries

- Status: Accepted for Phase 9 implementation
- Date: 2026-08-03
- Owners: Project maintainers
- Requirements: REQ-007, REQ-012, REQ-015, REQ-018, REQ-019
- Risks: R-043, R-044, R-045, R-046
- Verification: Phase 9 PostgreSQL, nginx/API, generated-contract, frontend,
  accessibility, keyboard, bundle and clean-copy evidence

## Context

The reviewer needs useful summary data, recent activity, overdue invoices and a
complete invoice discovery experience. A naïve implementation could load every
invoice and aggregate in memory, combine unrelated currencies, accept arbitrary
sort expressions, or expose tenant data through an insufficiently scoped join.
The UI also needs honest loading/error/no-result states without adding a charting
library for presentation alone.

Current EF Core documentation requires fully unique pagination ordering and
matching indexes. WCAG 2.2 requires labelled controls, visible focus, logical
focus order and programmatically determinable asynchronous status messages.

## Decision

- Use business-nested read endpoints and the existing database-backed Viewer
  authorization policy. Apply `BusinessId` before every join, count and sum.
- Invoice list input is allow-listed: bounded text search, known status, known
  sort key/direction, page at least one and page size 1-100. Default order is
  `UpdatedAtUtc DESC, Id ASC`; every alternative ends with `Id` for uniqueness.
- Use offset pagination because the product exposes small numbered pages and the
  reviewer needs page counts. Its deep-page cost is explicit; page size is capped
  and a composite tenant/order index supports the default path. Keyset pagination
  is the fallback if later evidence makes deep random access material.
- Dashboard aggregates stay in PostgreSQL. Counts are returned together, while
  outstanding and overdue amounts are grouped by currency. No exchange rate or
  cross-currency total is invented.
- Recent activity is limited to 10 and overdue invoices to 5, each uniquely
  ordered. Responses are projections, not tracked aggregates or navigation
  collections. Dashboard query limits are constants, not caller-controlled.
- Render semantic text, lists and links rather than a chart dependency. Vue Query
  owns cancellable server state; generated Zod schemas validate every response.
  Updating results retain the previous safe page and announce status without
  moving focus.

## Alternatives considered

- Aggregate all invoices in the API/browser: rejected as unbounded and contrary
  to the Phase 9 gate.
- Return one grand monetary total: rejected because currencies cannot be summed
  without an explicitly scoped conversion policy.
- Accept a raw SQL/order expression: rejected for injection and contract drift.
- Add Elasticsearch or PostgreSQL trigram search immediately: rejected because
  the assessment dataset and measured need do not justify another subsystem.
- Add a charting package: rejected because labelled metric cards and bounded
  lists communicate the required data with less bundle and accessibility cost.

## Consequences and failure modes

The API performs a small fixed set of database queries per dashboard request and
two queries per invoice page (count plus projection). Offset pagination grows in
cost with deep page numbers and concurrent writes can shift pages; unique ordering
prevents nondeterministic ties. A database outage yields the existing truthful
error state. An empty tenant yields zero counts and empty bounded arrays.

## Fallback or reversal

If query-plan or representative-load evidence later shows offset/search cost is
material, replace numbered navigation with a versioned keyset cursor and add an
evidence-backed PostgreSQL search index. If dashboard query complexity becomes
material, use a PostgreSQL read model refreshed from the existing outbox; do not
introduce a second source of financial truth.
