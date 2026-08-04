# ADR-006: Tenant authorization model

- Status: Accepted
- Date: 2026-07-31
- Owners: Project maintainers
- Requirements: REQ-002, REQ-003, REQ-015
- Risks: R-008, R-021, R-022
- Verification: V-0301, V-0302, V-0303, V-0304-R1, V-0305, V-0306

## Context

Identity proves who is making a request; it does not prove that the user belongs
to the business named by a route or may perform a business operation. Tenant
roles can be removed or changed while an authentication cookie remains valid.
The boundary must work identically after restart and across API replicas, avoid
revealing whether another tenant's resource exists, and remain understandable
before customer and invoice resources are added.

## Decision

Use ASP.NET Core Identity for users and password verification. Model tenancy with
a PostgreSQL-backed `BusinessMembership` identified by the composite key
`(BusinessId, UserId)`, an explicit active flag, and one business-scoped role:
Viewer, Accountant, or Owner.

Do not place tenant roles in the authentication cookie. For every endpoint that
addresses a business resource:

1. Require an authenticated principal.
2. Load the active membership using both the route business ID and current user
   ID.
3. Return 404 when that membership is absent or inactive, including cross-tenant
   IDs.
4. Pass the loaded membership to `IAuthorizationService` with the minimum role
   requirement.
5. Return 403 only when a current-tenant member lacks the required role.

The role order is Viewer < Accountant < Owner. The hierarchy is used only when
an endpoint genuinely accepts every higher role. PostgreSQL remains the source
of truth for every authorization decision.

Seed identities and memberships are local-review fixtures only. Seeding is
configuration-gated, requires an explicit password, and fails outside the
Development environment.

## Primary evidence

ASP.NET Core's resource-authorization documentation states that authorization
must be evaluated imperatively after a resource is loaded when the resource
affects the decision. Identity's documented cookie model and standard password
hashing are used rather than a custom credential store. Links are recorded in
`docs/evidence/primary-sources.md`.

Repository evidence is the EF migration, authorization handler, versioned API
endpoints, and `scripts/verify-phase2.ps1`. The script executes every Phase 2
matrix identity against PostgreSQL and the real Compose topology.

## Alternatives considered

- Global Identity roles or tenant-role claims in the cookie: rejected because a
  role for one business is not a role for every business, and claims remain
  stale after membership removal.
- Controller-only route checks: rejected because repeated ad hoc checks are easy
  to omit and do not express a reusable resource-authorization boundary.
- EF global query filters: rejected as the only boundary because they can hide
  accidental omissions but do not decide which operation a role may perform.
- One database or schema per tenant: rejected because the assessment scale does
  not justify its migration and operational cost.

## Consequences

Authorization costs a membership query for business-addressed requests. In
return, removal and role changes take effect on the next request without
reissuing the cookie. The explicit handler is reusable by later customer,
invoice, payment, and dashboard endpoints.

The current role hierarchy is deliberately small. There are no custom permission
sets, delegated administration rules, membership mutation API, or tenant-specific
identity providers in Phase 2.

## Failure modes

- Repeated seed execution is idempotent by stable IDs, unique Identity indexes,
  and the membership composite key; it updates the local fixture role/active
  state but does not reset existing passwords.
- Concurrent membership creation is protected by the composite primary key.
  Future membership mutation must add an explicit concurrency token and audit
  event before it becomes a product feature.
- A membership changed during an in-flight request can affect that request's
  snapshot but is re-read on the next request. No role is trusted from a stale
  cookie.
- Restart does not change authorization because users and memberships are in
  PostgreSQL. Replicas share the cookie key ring and application name, and each
  independently reads the same membership data.
- Missing, inactive, and cross-tenant membership all return 404 to reduce
  resource-enumeration disclosure. A same-tenant insufficient role returns 403.
- Lost data-protection keys invalidate sessions; unprotected local key storage is
  not evidence of a production key-protection mechanism.
- The login limiter bounds online attempts at this local topology. Distributed
  rate limiting is deferred until a multi-replica production ingress is in
  scope; authorization correctness does not depend on the limiter.

## Reversal or fallback path

If the ordered roles stop matching product permissions, replace minimum-role
comparison with named operation requirements while retaining the same loaded
membership resource. An external OIDC provider may replace local password
authentication without changing the business-membership boundary. A database-
per-tenant design requires a new ADR and migration proof; it is not a silent
extension of this decision.

## Verification

- `.\scripts\verify.ps1`
- `.\scripts\verify-phase2.ps1`
- `.\scripts\verify-compose.ps1`
- V-0301 through V-0307 and V-0399 in the verification ledger
