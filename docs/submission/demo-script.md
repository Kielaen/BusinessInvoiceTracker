# Reviewer Demonstration Script

Target duration: 12–15 minutes. Use the exact local identities and password in
`.env.example`; never copy them into a production environment.

## 1. Start and orient (2 minutes)

```powershell
docker compose --env-file .env.example up --build -d
.\scripts\verify-compose.ps1
docker compose --env-file .env.example ps
```

Open <http://127.0.0.1:8080>. Point out nginx as the only browser boundary,
PostgreSQL as source of truth, API/automation non-root containers, one-shot
migration, Dapr sidecars/control plane, Redis transport and Mailpit simulation.
The verifier should finish with an authenticated outbox/Dapr/duplicate-safe
delivery proof.

## 2. Security and tenant boundary (2 minutes)

Sign in as `owner@acme.test`. Show the HttpOnly same-site session behavior, the
active business context and role-based controls. Explain that every unsafe API
operation requires antiforgery proof and every resource query includes active
membership plus `BusinessId`. Cite the Phase 2/11 executable matrix rather than
claiming that hidden buttons provide authorization.

## 3. Full-stack invoice journey (4 minutes)

1. Create or select a customer; show validation and server-connected pagination.
2. Create a draft with two line items and mixed tax rates.
3. Show that totals returned by the server drive the preview.
4. Edit the draft, then issue it. Note the atomic number allocation, audit and
   lifecycle outbox commit.
5. Record a partial payment with a stable idempotency key, then complete payment.
6. Show status, outstanding amount and paginated audit history.

Explain the two important races: optimistic version conflicts prevent lost edits,
and database/idempotency constraints prevent duplicate or concurrent overpayment.

## 4. Overdue automation (3 minutes)

Use the existing Phase 8 verification fixture rather than waiting for a calendar
day:

```powershell
.\scripts\verify-phase8-happy-path.ps1
```

Show the invoice transition, audit event, one delivery record and Mailpit message.
Explain that the durable workflow waits until the Johannesburg due instant,
accepts payment/void/due-date external events, rereads PostgreSQL at timer fire,
and owns bounded delivery retry. Duplicate Pub/Sub, two publishers, restart,
reschedule, terminal races and reconciliation have separate executable scripts.

## 5. Failure and operations proof (2 minutes)

Show without changing retained state:

```powershell
Invoke-RestMethod http://127.0.0.1:8080/health/ready | ConvertTo-Json -Depth 5
Invoke-RestMethod http://127.0.0.1:5191/internal/operations/status | ConvertTo-Json -Depth 5
```

Point out process-only liveness, dependency-aware readiness, stale snapshot state,
publishable outbox backlog, separately retained domain-event history, workflow
commands/dead letters and notification outcomes. The destructive dependency
matrix belongs in an isolated rehearsal via `scripts/verify-clean-copy.ps1`, not
an improvised live demo.

## 6. Delivery evidence and trade-offs (2 minutes)

Open the public GitHub Actions run linked by the verification ledger. Show locked
build/test, generated/model drift, coverage, secret/dependency/final-image scans,
SBOMs, three image builds and Compose reviewer path. End with the architecture
summary, ADR alternatives and known limitations. State explicitly that no cloud
deployment, production SLO, penetration test, real email provider, legal tax
correctness or zero-downtime migration is claimed.

## Cleanup

Only when the demo data is disposable:

```powershell
docker compose --env-file .env.example down --volumes --remove-orphans
```
