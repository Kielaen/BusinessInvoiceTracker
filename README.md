# Business Invoice Tracker

Production-minded Software Engineer III assessment project built with .NET 10,
Vue, PostgreSQL, Dapr, nginx, and Docker Compose.

The repository is delivered through evidence-gated phases. The implemented
product includes tenant-scoped customers and invoices, server-authoritative
financial totals, issue/payment/void concurrency and idempotency, bounded
dashboard queries, Vue runtime contract validation, and durable Dapr overdue
notification with restart/retry/reconciliation evidence. Phase 10 and 11 add
truthful dependency health, operator diagnostics, security/abuse verification,
reviewed migration deployment, representative load, graceful shutdown,
secret/dependency/final-image scans, SBOMs and clean-copy rehearsal.

## Local reviewer start

Prerequisites: Docker Desktop with Linux containers and Docker Compose. The
Phase 11 supply-chain command also uses the Docker Scout CLI bundled with the
verified Docker Desktop installation.

```powershell
docker compose --env-file .env.example up --build -d
.\scripts\verify-compose.ps1
```

Open <http://127.0.0.1:8080>. Local demonstration identities and their explicitly
required password are listed in `.env.example`; the migration seed refuses to
run outside ASP.NET Core's Development environment.

Stop the stack and remove its local test data:

```powershell
docker compose --env-file .env.example down --volumes --remove-orphans
```

## Verification

Run locked backend restore/build, vulnerability checks, generated-client drift
generation, frontend tests/build/audit, and Compose validation:

```powershell
.\scripts\verify.ps1
```

Rebuild and verify the running topology and authenticated end-to-end path:

```powershell
.\scripts\verify-compose.ps1 -Build
```

Run the complete Phase 2 authorization, CSRF, seed-safety, and two-replica gate:

```powershell
.\scripts\verify-phase2.ps1 -Build
```

Run the customer API pagination, search, authorization, replay, validation, and
concurrency gate against the running stack:

```powershell
.\scripts\verify-phase5.ps1
```

Run the draft totals, replay, authorization, concurrency and audit gate:

```powershell
.\scripts\verify-phase6.ps1
```

Run the issue, payment, void, idempotency, authorization, audit, outbox and
concurrent-allocation/payment gate:

```powershell
.\scripts\verify-phase7.ps1
```

Run the Phase 11 authorization/abuse, migration, representative-load, graceful
shutdown, secret, final-image CVE, SBOM and non-root gates:

```powershell
.\scripts\verify-phase11.ps1
```

That focused suite includes a non-destructive backup/restore inventory check and
an isolated migrator-versus-application PostgreSQL privilege rehearsal. Temporary
databases and roles are uniquely named and removed; the retained database is not
replaced.

Recreate the current gates from copied source with build output excluded:

```powershell
.\scripts\verify-clean-copy.ps1
```

Optional local telemetry viewer:

```powershell
docker compose --env-file .env.example `
  -f compose.yaml -f compose.observability.yaml `
  --profile observability up --build -d
```

Then open <http://127.0.0.1:18888>.

Architecture, decisions, commands, risks, and truthful verification results are
under `docs/`. The binding engineering constitution and implementation plan are
under `docs/governance/`. Phase 11 deployment, migration, scan and finding
response details are in `docs/runbooks/phase-11-delivery.md`.
