# Phase 2 Working Plan

Status: **GATE PASSED**  
Started: 2026-07-31 (Africa/Johannesburg)  
Previous gate: Phase 1 passed as V-0299.

## Objective

Establish the authentication, business-membership, tenant-isolation and
resource-authorization boundary before any customer or invoice feature exists.

## Governing rules

All constitution rules remain binding. The most direct rules for this phase are
SE3-04, SE3-05, SE3-09, SE3-10, SE3-14, SE3-16, SE3-21 through SE3-25,
SE3-28 through SE3-31, and SE3-34.

## Work sequence

Only Phase 2 is active:

1. Replace the Phase 1 fixed credential principal with ASP.NET Core Identity
   backed by PostgreSQL and standard password hashing.
2. Add `Business` and `BusinessMembership` with stable IDs, explicit active
   state, business-scoped roles, foreign keys and uniqueness constraints.
3. Seed two businesses plus Owner, Accountant, Viewer, no-membership,
   cross-tenant and disabled-membership demonstration identities only when
   explicitly enabled with a required configured password.
4. Keep tenant roles out of the cookie. Resolve every resource authorization
   decision from the current membership row through `IAuthorizationService`.
5. Add versioned login/logout/current-user/business/membership contracts and
   update the Vue sign-in/sign-out flow through generated OpenAPI/Zod artifacts.
6. Add login rate limiting, safe API errors, cookie/antiforgery configuration and
   startup validation with no production secret fallback.
7. Execute the complete authorization matrix: anonymous, no membership, Viewer,
   Accountant, Owner, other tenant, and disabled membership.
8. Start a second API process against the same PostgreSQL database and
   data-protection key ring; prove its acceptance of the first process's cookie
   and antiforgery state.
9. Rerun locked builds, scans, image builds, clean-copy startup, and reconcile
   evidence, risks, traceability, ADRs and reviewer commands.

## Gate

- Anonymous, wrong-role, and cross-tenant tests pass.
- CSRF rejection is demonstrated.
- Two API replicas share valid authentication state.
- No production secret fallback exists.

## Scope guard

Phase 2 does not add customers, invoices, payments, overdue workflows or
dashboard queries. Membership mutation is limited to what is required to prove
the authorization boundary; tenant-owned product resources begin in later
phases.

## Completed result

ASP.NET Core Identity, persisted business memberships, resource authorization,
same-origin Vue login/logout, generated runtime contracts, explicit local-only
demonstration seeding, and the two-replica data-protection topology are
implemented. `scripts/verify-phase2.ps1` passed every gate row, including missing
secret and Production seed refusal. See V-0300 through V-0399.

No Phase 3 implementation began before this gate passed.

## Phase report

- Completed: Identity persistence and standard password hashing; business and
  active-membership model; Owner/Accountant/Viewer resource authorization;
  Development-only seed; login/logout/current-user Vue flow; shared key ring;
  authorization and clean-copy verification scripts.
- Constitution applied: SE3-01 evidence over claims; SE3-04 failure analysis;
  SE3-05 restrained design; SE3-12 authentication is not authorization; SE3-13
  no production secret defaults; SE3-14 cookie threat model; SE3-16 generated
  contract; SE3-23 replica correctness; SE3-27 through SE3-30 risk-based,
  production-shaped and clean-copy testing; SE3-34 executable documentation.
- Evidence passed: V-0300 through V-0303 and V-0304-R1 through V-0309.
- Decision: ADR-006 accepted; ADR-005 remains the governing cookie/antiforgery
  decision.
- Remaining risks: remote CI execution (R-020), production key encryption and
  bootstrap deployment work, and per-endpoint matrix coverage as later resource
  types are added.
- Gate: passed as V-0399.
- Next: Phase 3, domain and persistence foundation.
