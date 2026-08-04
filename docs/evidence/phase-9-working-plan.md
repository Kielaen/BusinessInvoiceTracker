# Phase 9 Working Plan

Status: **PASSED**  
Started: 2026-08-03 (Africa/Johannesburg)  
Completed: 2026-08-03 (Africa/Johannesburg)  
Previous gate: Phase 8 passed as V-0913.

## Objective

Complete the reviewer-visible dashboard, invoice discovery and activity
experience without loading unbounded tenant data into the API process or browser.

## Governing rules

The constitution and implementation plan remain binding. PostgreSQL is the
authoritative source; every query is tenant-scoped before aggregation; monetary
values with different currency codes are never combined; frontend responses are
runtime-validated; and no control is rendered unless it performs a proven action.

## Decisions and risks to prove

- Invoice filtering, search, sorting and pagination need allow-listed semantics,
  fully unique ordering and fixed maximum page size.
- Dashboard counts and currency totals must execute as server-side aggregates.
  Recent activity and overdue lists require explicit small limits.
- Cross-tenant identifiers must remain indistinguishable from missing resources.
- Search/count queries must not materialize full entities or collections.
- Loading, empty, error, updating and no-results states must remain distinct.
- Keyboard order, visible focus, labels, result announcements and responsive
  reflow require both automated and manual evidence.
- The existing JavaScript budget remains the gate; no charting dependency is
  justified for a small metric summary.

## Work sequence

Only Phase 9 is active:

1. Record query semantics, primary-source evidence, risks and ADR-017.
2. Implement tenant-scoped invoice discovery and dashboard query endpoints.
3. Regenerate the checked OpenAPI TypeScript client and Zod schemas.
4. Implement the dashboard and complete invoice controls with responsive,
   accessible states.
5. Prove tenant isolation, bounds, filters, deterministic ordering, metrics,
   activity, functional controls, automated accessibility and keyboard flow.
6. Run prior regressions, deterministic generation, bundle and clean-copy gates;
   reconcile evidence before deciding the Phase 9 gate.

## Gate

- No unbounded dashboard query.
- No non-functional controls.
- Automated accessibility checks pass.
- The manual keyboard script passes.
- The bundle stays within the documented budget or has an explicit justification.

## Scope guard

Phase 9 does not add charting infrastructure, full-text search services, exports,
PDFs, observability hardening or load claims. Those do not earn their cost here.

## Gate result

V-1000 through V-1004 and V-1005-R1 pass. The bounded PostgreSQL queries,
cross-tenant checks, generated contract, frontend tests, live nginx journey,
responsive reflow, bundle budget, full clean-copy run and user-executed manual
keyboard script are proven. V-1099-R1 closes the Phase 9 gate. Phase 10 is next.
