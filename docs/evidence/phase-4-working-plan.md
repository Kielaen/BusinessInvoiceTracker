# Phase 4 Working Plan

Status: **GATE PASSED**  
Started: 2026-07-31 (Africa/Johannesburg)  
Previous gate: Phase 3 passed as V-0499.

## Objective

Establish one trustworthy, versioned OpenAPI boundary and a production-minded
Vue application shell before product feature slices begin.

## Governing rules

All constitution rules remain binding. The most direct rules for this phase are
SE3-01, SE3-04, SE3-09, SE3-10, SE3-14 through SE3-20, SE3-23 through SE3-25,
SE3-27 through SE3-30, SE3-32, and SE3-34.

## Work sequence

Only Phase 4 is active:

1. Make API errors consistently use RFC 7807 Problem Details and document
   meaningful status codes in the generated OpenAPI contract.
2. Regenerate the exact-pinned TypeScript client and Zod schemas and prove
   generation is deterministic with zero drift.
3. Configure Vue Router with lazy route modules, TanStack Vue Query with bounded
   safe-query retry and mutation retry disabled, and Pinia only for genuine
   cross-route workspace selection.
4. Build a semantic application shell, accessible authentication flow, complete
   loading/empty/error/success states, and root plus feature error boundaries.
5. Add contract, malformed-payload, problem-state, keyboard, accessibility, and
   bundle-budget verification.
6. Run the full locked build, tests, scans, Compose regression, and clean-copy
   checks; reconcile evidence, traceability, risks, and this phase report.

## Gate

- Generated OpenAPI/TypeScript/Zod artifacts have zero drift.
- A malformed successful API payload is visibly and safely rejected.
- Validation, 401, 403, 404, 409, and unexpected failures render the correct
  recovery state.
- Keyboard navigation works through the shell and authentication flow.

## Scope guard

Phase 4 supplies contract and presentation foundations. Customer, invoice,
payment, dashboard, workflow, and reporting product behavior remains in later
phases. No placeholder endpoint will be added merely to manufacture a status.

## Completed result

The API now publishes one versioned contract with consistent Problem Details,
documented response statuses, generated SDK/types/Zod schemas, and deterministic
drift verification. The Vue client uses lazy routing, a bounded Vue Query policy,
minimal workspace-only Pinia state, semantic authentication and application
shells, complete recovery states, and root plus feature error containment.

## Phase report

- Completed: versioned antiforgery and API errors; generated client boundary;
  query/router/store foundation; accessible shell and login; retained delivery
  diagnostic; explicit states; drift, bundle, accessibility and clean-copy gates.
- Constitution applied: SE3-01 evidence over claims; SE3-04 failure analysis;
  SE3-09/10 explicit boundaries; SE3-14 cookie/CSRF preservation; SE3-16 one
  generated contract; SE3-17 bounded retry semantics; SE3-18 correct state
  ownership; SE3-19 complete states and containment; SE3-20 semantic keyboard,
  accessibility, lazy routes and budget; SE3-27 through SE3-30 risk-shaped real
  tests and clean-copy proof; SE3-34 executable documentation.
- Evidence passed: V-0500 through V-0508.
- Decisions: ADR-010 remains the accepted OpenAPI/client/runtime-validation
  decision. No new ADR was necessary; implementation followed its proven path.
- Remaining risks: each later feature must add its own tenant/error/contract/UI
  matrix; API-level optimistic conflict creation remains Phase 6; remote CI
  remains R-020; production data-protection key encryption remains deployment work.
- Gate: passed as V-0599.
- Next: Phase 5, customer vertical slice.

No Phase 5 implementation began before this gate passed.
