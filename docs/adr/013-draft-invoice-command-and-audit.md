# ADR-013: Draft invoice command, preview, and audit semantics

- Status: Accepted
- Date: 2026-07-31
- Owners: Project maintainers
- Requirements: REQ-005, REQ-006, REQ-008, REQ-015, REQ-018
- Risks: R-024, R-025, R-026, R-031
- Verification: Phase 6 API, PostgreSQL, frontend and E2E evidence

## Context

Phase 6 must expose the existing invoice aggregate without duplicating financial
rules in controllers or Vue. Draft identity, duplicate submission, numbering,
audit content, and preview behavior must be explicit before mutable endpoints
exist.

## Decision

- Drafts are tenant-owned and use the route business plus a freshly loaded
  active membership. Viewer reads; Accountant and Owner create and edit.
- Create uses a client-generated stable invoice ID. An identical replay returns
  the existing draft; reuse with different normalized content returns 409.
- Drafts have no invoice number. Number allocation belongs to the atomic issue
  command in Phase 7, matching the implementation plan and ADR-009's snapshot
  boundary. This amends ADR-007's premature Phase 6 wording without changing its
  allocation algorithm.
- Requests provide customer, `ZAR` currency, issue/due dates, and one to 200
  lines. The server constructs `Money`, `TaxRate`, and `InvoiceLineDraft` values;
  only aggregate-calculated net, tax, total, paid and outstanding values are
  returned and displayed.
- Updates require the last observed GUID version. Stale writes return 409 and
  the UI offers a reload path; no last-write-wins fallback exists.
- Create and material edit append audit entries in the same database transaction.
  Audit JSON contains action, version and calculated totals, never customer
  contact data or line descriptions.
- Preview is the persisted draft detail representation. Vue may format returned
  values but never recompute authoritative totals.

## Alternatives considered

- Allocate a number on draft creation: rejected because abandoned drafts would
  consume visible numbers and the governing Phase 7 gate assigns atomic
  allocation to issue.
- Calculate totals in Vue: rejected because clients and replicas could disagree
  and manipulated values could be submitted.
- Store a full before/after invoice document in audit JSON: rejected because it
  duplicates sensitive customer and description content without a requirement.
- Generic command infrastructure: deferred until Phase 7 has a second command
  requiring stored replay semantics.

## Consequences and failure modes

Draft responses always reconcile line and invoice totals. Invalid scale, range,
currency, dates, customer ownership, or line count fail before commit. Duplicate
IDs, tenant email knowledge, and stale versions do not leak another tenant.
Database failure rolls back invoice, lines, and audit together. Multiple replicas
remain safe because identity, uniqueness, foreign keys and versions are stored in
PostgreSQL.

## Fallback or reversal

If a legal numbering policy later requires reservation at draft time, add an
explicit reservation/expiry ledger and migration ADR. If richer audit content is
required, version the audit schema and document retention/access policy first.
