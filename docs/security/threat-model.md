# Submission Threat Model

Status: active assessment model, reviewed 2026-08-04. This is a focused design
and verification artifact, not a penetration-test or compliance report.

## Scope and assets

The protected assets are Identity credentials/session cookies, tenant
memberships, customers, invoices, payments, audit history, outbox/inbox state,
workflow commands/state, delivery attempts and data-protection keys. Customer
contact and invoice line content are personal/business data and must not enter
routine telemetry.

The public assessment boundary is nginx on port 8080. PostgreSQL, Redis, Dapr
sidecars/control plane, Mailpit and the automation management endpoint are local
topology dependencies, not public APIs. The replica port and Mailpit UI exist
only in explicit local test profiles.

## Actors and trust boundaries

- Anonymous internet-style caller: may fetch the application shell,
  antiforgery bootstrap and login; receives no tenant resource.
- Authenticated Viewer: tenant-scoped reads only.
- Authenticated Accountant: tenant-scoped customer/invoice/lifecycle mutations.
- Authenticated Owner: Accountant capabilities plus membership administration.
- Another tenant or inactive/no-membership user: indistinguishable tenant-owned
  resource responses; no data or existence disclosure.
- Local operator/deployer: owns secrets, migration privileges, Docker topology
  and loopback diagnostics. These privileges are not granted to browser users.
- Compromised dependency/process: assumed able to fail, retry or replay work;
  PostgreSQL constraints, stable identities and durable boundaries limit effects.

Browser input crosses nginx into cookie-authenticated ASP.NET endpoints.
Application state crosses into PostgreSQL. Lifecycle events cross API outbox,
Dapr Pub/Sub, automation inbox/command and Dapr Workflow boundaries. Simulated
delivery crosses SMTP into Mailpit. Each crossing treats IDs, tenant, totals,
roles, status and delivery outcomes as untrusted until its owning layer validates
or rereads authority.

## Threat and control matrix

| Threat | Prevent/detect control | Executable evidence | Residual limitation |
|---|---|---|---|
| Credential guessing/account enumeration | Identity hashing/lockout, generic failed sign-in, fixed-window login limiter | Phase 11 bounded-window/429 test; Phase 2 seed and auth tests | Local limiter is per process; a distributed edge limiter is deployment work |
| Session theft/fixation | HttpOnly, SameSite=Strict, 30-minute non-sliding cookie, fresh Identity sign-in, shared protected keys | Phase 11 cookie assertions; V-0305 replicas | Local HTTP cannot assert `Secure`; Production configuration requires HTTPS and `__Host-` cookies |
| CSRF | Same-origin deployment, strict cookie and request antiforgery token on every unsafe browser endpoint | Phase 2/4 and Phase 11 missing-token rejection | XSS could act as the user; CSP and output encoding reduce but do not eliminate that class |
| XSS/clickjacking/content confusion | Vue text binding, consumer DTOs, CSP, `nosniff`, `frame-ancestors`/DENY, no inline application scripts | Phase 11 root/API header checks; frontend rendering tests | No browser extension or third-party script threat is modeled |
| Broken function authorization | Policy/resource authorization after database membership read; mutation roles explicit | V-0302/0602/0702/0803 and Phase 11 rerun | New endpoints must extend the matrix before release |
| IDOR/cross-tenant disclosure | BusinessId predicate and active membership before resource projection; inaccessible IDs return 404 | Cross-tenant customer/invoice/dashboard/lifecycle tests | PostgreSQL row-level security is not used; the application policy is the primary boundary |
| Mass assignment/financial tampering | Explicit request DTOs; server recalculates totals and owns status/number/version | Domain/API/concurrency evidence V-0402 through V-0804 | Tax is a documented demonstration model, not legal advice |
| Duplicate/racing commands | Stable IDs/idempotency keys, unique constraints, optimistic concurrency, atomic transactions | V-0404/5, V-0702/5, V-0802, V-0905/7/10 | At-least-once transport remains intentional and observable |
| Injection/search abuse | EF parameterization, explicit wildcard escaping, allow-listed sort/filter, bounded lengths/pages | V-0602/4 and V-1001 | This phase does not claim general-purpose DAST coverage |
| Resource exhaustion | Page/line/query bounds, cancelled searches, login limiting, fixed dependency timeouts and bounded retries | V-0604, V-0701, V-1001, V-1102/3 and Phase 11 load profile | No production DDoS protection or SLO is claimed |
| Secret exposure | No production fallback, ignored local environment/runtime state, clean-material and committed-history Gitleaks scans, PII-safe telemetry | V-0306, Phase 10 instrumentation review, V-1206, V-1211/V-1212 | Scans are time-scoped; managed deployment secrets and key rotation remain environment responsibilities |
| Vulnerable dependency/image | Exact locks/digests, NuGet/npm audits, distinct final-image CVE scans and SBOMs | Phase 11 supply-chain gate | Advisory data changes; a passing scan is time-scoped, not a future guarantee |
| Unsafe schema deployment | One-shot migrator before apps, pending-model check, reviewed idempotent SQL and snapshot upgrades | ADR-019 and Phase 11 migration gate | Backup/restore and least-privilege identities depend on the real deployment environment |
| Async work lost on shutdown | PostgreSQL/Dapr durable ownership, cancellation, stable attempts, restart reconciliation | Phase 8/10 plus Phase 11 stop/restart proof | Forced host/volume loss is outside graceful shutdown and needs infrastructure recovery |
| Diagnostic surface disclosure | Operations endpoint bound to loopback; metrics have bounded labels; logs omit contact/content | V-1104 and instrumentation review | A deployed management plane requires separate authentication and network policy |

## Abuse cases retained for every new endpoint

For anonymous, Viewer, Accountant, Owner, inactive member, no member and another
tenant, test both the function and a supplied foreign resource ID. For every
unsafe cookie request, test absent/invalid antiforgery proof. For every important
command, test exact replay, changed-payload identity reuse, stale/concurrent
execution, timeout/restart and multiple replicas. Collections must test maximum
page/input bounds and allow-listed ordering. Responses and telemetry must not
echo secrets, credentials, customer contact or invoice content unnecessarily.

## Accepted deployment responsibilities

TLS termination, `Secure` production cookies, encrypted data-protection keys,
database backup/restore, separate migration credentials, network segmentation,
distributed edge rate limiting, WAF/DDoS controls, vulnerability monitoring and
an authenticated operator plane depend on a chosen hosting environment. They are
not silently represented by local Compose. Production mode fails closed for
missing demo configuration and enables HSTS/secure cookie behavior, but a real
HTTPS deployment remains outside this assessment.
