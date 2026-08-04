# Business Invoice Tracker

A production-minded Software Engineer III full-stack assessment built with
.NET 10, Vue, PostgreSQL, Dapr, nginx and Docker Compose. Businesses can manage
tenant-scoped customers and invoices, issue and pay invoices safely, inspect
history and receive a simulated durable overdue notification.

“Production-minded” here means the implemented assessment boundaries have
runnable evidence for security, correctness, failure behavior, diagnostics and
delivery. It does not mean this local Compose stack is represented as a real
production deployment. See [known limitations](docs/submission/known-limitations.md).

## Reviewer start

Prerequisites:

- Docker Desktop using Linux containers with Docker Compose.
- PowerShell 7 or Windows PowerShell 5.1 for repository verification scripts.
- The focused Phase 11 supply-chain gate uses the Docker Scout CLI bundled with
  the verified Docker Desktop installation.

From a clean clone:

```powershell
docker compose --env-file .env.example up --build -d
.\scripts\verify-compose.ps1
```

Open <http://127.0.0.1:8080>. Demonstration identities and their required local
password are in `.env.example`. The seed fails when the password is omitted and
refuses to run outside ASP.NET Core Development.

Remove local containers and disposable demonstration data:

```powershell
docker compose --env-file .env.example `
  -f compose.yaml -f compose.observability.yaml `
  --profile observability down --volumes --remove-orphans
```

## Product journeys

- Sign in/out through same-origin HttpOnly cookie authentication and antiforgery.
- Switch active business context with Owner, Accountant or Viewer permissions.
- Create, validate, search, paginate, inspect and edit tenant-owned customers.
- Create/edit draft invoices with dynamic lines and server-authoritative totals.
- Issue invoices, record idempotent partial/full payments, void allowed invoices
  and reject stale, invalid, duplicate-conflicting or concurrent overpayment.
- Filter, sort, search and paginate invoices; inspect bounded dashboard data and
  paginated audit history.
- Run a durable overdue timer, react to payment/void/due-date events, simulate
  delivery through Mailpit and recover/reconcile duplicates, retries and restarts.

## Architecture

```text
Browser :8080 -> unprivileged nginx -> ASP.NET Core API -> PostgreSQL
                                      -> Dapr/Redis Pub/Sub
                                         -> automation host + Dapr Workflow
                                            -> PostgreSQL + Mailpit
Optional .NET telemetry -> local Aspire dashboard :18888
```

The API is a modular monolith for synchronous financial behavior. A separate
automation host owns durable timing and simulated notification failure. Database
state, audit and outbox commit atomically; broker/workflow/delivery steps are
eventual, idempotent and reconciled. The full design and alternatives are in the
[architecture and trade-off summary](docs/architecture/submission-architecture-and-tradeoffs.md)
and [ADRs](docs/adr/README.md).

## Services and ports

| Surface | Local address | Purpose |
|---|---|---|
| nginx/Vue/API | <http://127.0.0.1:8080> | Only public reviewer boundary |
| Mailpit UI | <http://127.0.0.1:8025> | Simulated email inspection |
| Automation management | <http://127.0.0.1:5191> | Loopback health/reconciliation/operations only |
| Automation Dapr HTTP | <http://127.0.0.1:5192> | Loopback test diagnostics only |
| Optional Aspire | <http://127.0.0.1:18888> | Disposable unauthenticated local telemetry viewer |

PostgreSQL, Redis, placement and scheduler have no host port in the default
topology. The API replica port exists only under the `replica-test` profile.

## Security model

- ASP.NET Core Identity cookie: HttpOnly, SameSite Strict, non-sliding 30-minute
  lifetime; non-development uses Secure `__Host-` cookies and HSTS.
- Every unsafe browser endpoint validates an antiforgery header.
- Every tenant resource rereads active membership and authorizes the minimum
  Owner/Accountant/Viewer role; foreign or inaccessible resource IDs return 404.
- CSP and defensive response headers are emitted by nginx/API; demo secrets have
  no production fallback; images run non-root where applicable.

This is a tested local threat model, not a penetration-test or compliance claim.
See [threat model](docs/security/threat-model.md).

## Verification

Run locked restore/build, formatting, tests/coverage floors, dependency audits,
generated-contract drift, Vue production build/bundle budget and Compose parsing:

```powershell
.\scripts\verify.ps1
```

Run the focused authorization/abuse, migration/deployment, representative local
load, graceful shutdown, secret, final-image critical/high, SBOM and non-root
gate against the retained stack:

```powershell
.\scripts\verify-phase11.ps1
```

Run the definitive unattended reviewer rehearsal from copied source and fresh
volumes. It executes the retained Phase 2 and Phase 4–11 gates, then removes its
isolated resources and restores the main stack:

```powershell
.\scripts\verify-clean-copy.ps1
```

Evidence includes real PostgreSQL concurrency and migration tests, two API
replicas, generated runtime-schema rejection, Dapr timer/event/restart/duplicate/
retry/reconciliation tests, dependency health failure/recovery, accessibility,
clean source scans, final-image scans and hosted CI. Exact commands, failures and
passes are retained in the [verification ledger](docs/evidence/verification.md)
and mapped in [requirements traceability](docs/evidence/requirements-traceability.md).

## Health and operations

```powershell
Invoke-RestMethod http://127.0.0.1:8080/health/ready | ConvertTo-Json -Depth 5
Invoke-RestMethod http://127.0.0.1:5191/health/ready | ConvertTo-Json -Depth 5
Invoke-RestMethod http://127.0.0.1:5191/internal/operations/status | ConvertTo-Json -Depth 5
```

`/health/live` is process-only. Readiness checks required PostgreSQL/Dapr/control
plane dependencies with time bounds. Mailpit loss is `degraded` because durable
intake can continue. Operational status exposes staleness, publishable outbox
backlog, separately retained domain-event history, workflow commands/dead letters
and notification outcomes. Use the [operations runbook](docs/runbooks/phase-10-operations.md)
for bounded queries and correlation.

Start the optional viewer without making it a business dependency:

```powershell
docker compose --env-file .env.example `
  -f compose.yaml -f compose.observability.yaml `
  --profile observability up --build -d
```

Then open <http://127.0.0.1:18888>. JSON stdout and health remain the default.

## Dapr purpose

Dapr owns two real distributed concerns: Redis-backed at-least-once lifecycle
delivery and a PostgreSQL-backed durable overdue workflow. Exact runtime 1.17.7
and .NET SDK 1.17.9 behavior was proven for timer restart, early external events,
duplicate delivery, multiple publishers, notification retry, terminal races and
reconciliation. Dapr is not used for the business database or as architectural
decoration; ADR-003 retains the Hangfire fallback.

## Troubleshooting

- API unready: inspect PostgreSQL, the completed `migrate` service, then `api` and
  `api-dapr` logs. Recreate an app and its shared-network sidecar as a pair.
- Automation unready: inspect PostgreSQL, its Dapr sidecar, placement and scheduler.
  Mailpit-only loss should be degraded, not unready.
- Browser API returns 502: verify `api`/`api-dapr` health and nginx Docker DNS;
  static Vue assets may remain available while the API is stopped.
- Pending work: inspect `/internal/operations/status`, then use the bounded SQL
  queries and correlation procedure in the operations runbook. Never mark durable
  rows successful manually.
- Build drift: run `scripts/verify.ps1`; do not hand-edit generated files.

Delivery/migration/scan details are in the
[Phase 11 runbook](docs/runbooks/phase-11-delivery.md). The reviewer walkthrough
and evidence-based defence are in the [demo script](docs/submission/demo-script.md)
and [interview bank](docs/submission/interview-question-bank.md).
