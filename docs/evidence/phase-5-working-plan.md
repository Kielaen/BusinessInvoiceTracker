# Phase 5 Working Plan

Status: **GATE PASSED**  
Started: 2026-07-31 (Africa/Johannesburg)  
Previous gate: Phase 4 passed as V-0599.

## Objective

Deliver customer create, list, detail, and edit as the first complete tenant-
scoped product slice, with real PostgreSQL behavior and a usable Vue journey.

## Governing rules

All constitution rules remain binding. The most direct are SE3-01, SE3-04,
SE3-06 through SE3-08, SE3-10 through SE3-12, SE3-15 through SE3-20, SE3-23,
SE3-25, SE3-27 through SE3-30, SE3-32, and SE3-34. ADR-006, ADR-010, and ADR-011
govern tenancy, contracts, and customer semantics.

## Work sequence

Only Phase 5 is active:

1. Record customer validation, lifecycle, duplicate-create, concurrency,
   deletion, search, and pagination semantics in ADR-011.
2. Extend the domain and PostgreSQL schema with bounded billing/contact fields,
   checks, indexes, and the existing GUID concurrency token.
3. Add versioned create/list/detail/edit endpoints with Viewer versus
   Accountant/Owner authorization, Problem Details, cancellation, bounded
   pagination, escaped search, structured telemetry, and generated contracts.
4. Build lazy customer list/detail/create/edit views using Vue Query server
   state, local form state, runtime response validation, cancellable debounced
   search, real paging controls, and complete UI states.
5. Add domain, real PostgreSQL, API, frontend, accessibility, keyboard, and
   Compose E2E evidence, including cross-tenant and stale-write failures.
6. Run locked, regression, migration, generated-drift, clean-copy, and security
   gates; reconcile documentation and risk evidence.

## Gate

- Every constitution implementation-gate item is evidenced for customers.
- Cross-tenant customer access is blocked without existence disclosure.
- Search is debounced and obsolete requests are cancelled.
- Pagination controls drive bounded server pagination.

## Scope guard

Phase 5 does not create invoices, expose physical customer deletion, add generic
idempotency infrastructure, or implement full-text-search services.

## Completed result

The repository now provides tenant-scoped customer create, list, detail and edit
through versioned ASP.NET endpoints and generated TypeScript/Zod clients. The
Vue journey uses cancellable debounced search, bounded server pagination,
runtime response validation, optimistic conflict handling, accessible forms and
complete boundary states. PostgreSQL enforces the tenant email, billing and
concurrency invariants defined by ADR-011.

## Phase report

- Completed: customer domain/schema/migration; Viewer read and
  Accountant/Owner mutation endpoints; duplicate-create and stale-edit handling;
  search/pagination; lazy Vue views; generated contracts; telemetry; automated
  and browser E2E verification.
- Constitution applied: SE3-01/04 evidence and retained failures; SE3-06 through
  SE3-08 domain/database invariants; SE3-10 through SE3-12 tenant boundaries,
  concurrency and replay safety; SE3-15 through SE3-20 contract, state,
  accessibility and bundle discipline; SE3-23/25 observable safe failure;
  SE3-27 through SE3-30 risk-shaped PostgreSQL/Compose/clean-copy tests; SE3-32
  truthful scope; SE3-34 executable documentation.
- Evidence passed: V-0600 through V-0608, with V-0603 retained as a failed live
  contract check and V-0603-R1 proving the correction.
- Decisions: ADR-011 accepted customer ownership, roles, validation, stable-ID
  replay, GUID concurrency, no Phase 5 deletion, escaped `ILIKE`, and bounded
  uniquely ordered offset pages.
- Remaining risks: remote CI remains R-020; production data-protection key
  encryption remains deployment work; offset paging is accepted only at
  assessment scale; archive/retention policy and generic idempotency remain
  intentionally deferred.
- Gate: passed as V-0699.
- Next: Phase 6, draft invoice vertical slice.

No Phase 6 implementation began before this gate passed.
