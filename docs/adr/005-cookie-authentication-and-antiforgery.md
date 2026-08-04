# ADR-005: Cookie authentication and antiforgery

- Status: Accepted
- Date: 2026-07-30
- Requirements: REQ-002, REQ-003, REQ-015
- Risks: R-007, R-008
- Verification: V-0104, V-0105

## Context

The Vue SPA and API can share an nginx origin. Cookie authentication keeps browser
credentials out of JavaScript, but every unsafe request then needs CSRF defence.
Multiple API replicas must read the same protected ticket.

## Decision

Use ASP.NET Core Identity cookie authentication in product code. Use HttpOnly,
SameSite=Strict, Secure production cookies; explicit API 401/403 responses; an
antiforgery cookie plus `X-CSRF-TOKEN` header on every unsafe endpoint; and a
shared data-protection key ring/application name. nginx serves the SPA and proxies
`/api` on the same origin. Authorization is server-side and tenant membership is
rechecked for protected resources.

The Phase 0 fake credential endpoint proves protocol behaviour only; it is not the
product identity store.

## Primary evidence

ASP.NET Core's cookie API, antiforgery and data-protection documentation is linked
in `docs/evidence/primary-sources.md`. `verify-local.ps1` proves 401 without
redirect, cookie use across two API replicas, missing-CSRF rejection,
cross-replica antiforgery, and 403 for the wrong role.

## Alternatives considered

- Browser-stored bearer tokens: rejected because XSS exposes credentials and the
  assessment does not require cross-origin API clients.
- OIDC Authorization Code + PKCE: fallback if an external identity provider
  becomes a requirement; operationally excessive now.
- Single-replica keys: rejected because scale-out invalidates sessions.

## Consequences and failure modes

Key-ring loss invalidates sessions. Concurrent first boot against an empty
file-system ring was not proven; deployment must initialize/protect shared keys.
Local spike keys are unencrypted and are not production evidence. CSRF validation
fails closed with RFC Problem details. Authentication does not substitute for
tenant authorization.

## Reversal or fallback path

Adopt OIDC Code+PKCE only with a proven provider/topology and preserve server-side
authorization. If nginx same-origin routing fails V-0104, this ADR must reopen.

## Verification

Local protocol, two-process key sharing, and nginx same-origin Compose checks pass.
The Phase 0 topology is intentionally HTTP-only local evidence; production HTTPS
and protected shared-key storage remain Phase 1/2 requirements. See V-0104A,
V-0104B-R1, and V-0105.
