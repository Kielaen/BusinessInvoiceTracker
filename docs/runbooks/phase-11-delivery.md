# Phase 11 Security and Delivery Runbook

This runbook covers the local assessment and CI evidence. It does not replace a
hosting-environment security, backup, restore or rollout procedure.

## Focused gate

With the default Compose stack running:

```powershell
.\scripts\verify-phase11.ps1
```

The orchestrator reruns the retained tenant/role/CSRF matrix, checks same-origin
headers and cookie attributes, exercises the login limiter, verifies migration
artifacts/repeat behavior, runs bounded authenticated reads, stops asynchronous
publishing during a dependency failure, scans clean source/final images,
generates temporary SPDX SBOMs and inspects final image users. It intentionally
changes local limiter/process state and restores the app/sidecar pair afterward.

The full isolated reviewer rehearsal remains:

```powershell
.\scripts\verify-clean-copy.ps1
```

`verify-phase11-database-operations.ps1` is also part of the focused suite. It
creates uniquely named temporary databases and roles, takes a custom-format
backup of the retained application database, restores it without replacing the
live database, and compares stable application inventory. It then proves a
deployment pattern in which a migration identity applies the previous and
current schemas while a separate steady-state identity can read/write data but
cannot create schema. The guarded `finally` block removes only those unique
temporary databases, roles, SQL artifacts and container backup file.

## Security finding triage

Do not suppress or lower a finding to pass the gate. Capture:

1. scanner/tool and advisory snapshot date;
2. artifact digest, package and installed/fixed versions;
3. reachability and affected feature;
4. vendor advisory and supported remediation;
5. fix evidence, or an explicit ADR/risk with owner and expiry.

Critical/high findings block Phase 11 until fixed or proven non-applicable with
inspectable evidence. “Unfixed” is not itself a waiver. Secret findings must be
treated as exposed until scope is established; rotate real credentials before
removing them from source/history. `.env.example` contains local-only reviewer
values and is not a production secret source.

The local Gitleaks check scans clean copyable material because runtime keys,
dependencies and build output are deliberately excluded from the repository.
CI separately scans committed Git history. The first broad local scan correctly
identified a generated ignored data-protection key; that failure is retained in
the verification notes and was not converted into a clean-source claim.

## Migration deployment

Normal API/automation startup never owns schema changes. For local Compose,
`migrate` is a finite service and API containers depend on its successful exit.
Before a deployment:

```powershell
.\scripts\verify.ps1
.\scripts\verify-phase11-migrations.ps1
```

CI generates `artifacts/migrations.sql` with `--idempotent` for review. Review
locks, data rewrites, nullable/default transitions, index creation and every
destructive statement against representative data. Take and verify a database
backup before applying a production migration. Record the application versions
compatible with both the pre- and post-migration schema.

Rollback is restore-and-redeploy: restore the verified backup and deploy the
last compatible application/schema pair. The repository does not promise a safe
automatic down-migration. The executable rehearsal proves that schema privileges
can be restricted to a migration job while a steady-state application identity
retains DML but receives PostgreSQL `permission denied` for schema creation.
Default Compose deliberately keeps one local-only reviewer credential; a hosting
environment must provision the demonstrated identities through its secret and
database administration boundary.

The current latest migration is index-only. The rehearsal applies the previous
schema, performs application-role DML, applies the full idempotent chain as the
migrator, performs DML again, and requires the main stack to remain ready. This
is evidence of compatibility for this exact small local migration. It is not a
universal zero-downtime or production lock-duration claim.

If API containers are rebuilt in the local shared-network Compose topology,
recreate their paired Dapr sidecars. Starting an old sidecar container after the
app container identity changes cannot join the new network namespace.

## Performance interpretation

`verify-phase11-performance.ps1` executes 120 authenticated bounded reads with a
maximum concurrency of eight and reports p50/p95/max, wall time and errors. The
2-second p95 and 5-second maximum are diagnostic regression bounds chosen to
catch obvious local failures. They are not an SLO, capacity plan, saturation
test or internet-scale claim. Preserve raw numbers and environment details when
comparing runs. Existing PostgreSQL concurrency tests, rather than this read
profile, remain the proof for write races.

## Graceful shutdown

`verify-phase11-shutdown.ps1` makes Redis unavailable, inserts a uniquely marked
durable outbox batch, waits for real attempts, sends Compose's bounded graceful
stop to the API/sidecar pair, checks retained rows, restores Redis/processes and
requires one receipt per row with bounded attempts. It never deletes retained
volumes. Failure response:

- leave durable rows unchanged;
- restore the failed dependency;
- recreate the current app/sidecar pair if container identity changed;
- inspect outbox attempts/leases and inbox receipts with the Phase 10 runbook;
- use reconciliation rather than direct success-state edits.

The Phase 8 workflow retry/restart tests remain the stronger automation-specific
proof. Graceful host shutdown cannot protect against lost database/workflow
volumes; infrastructure backup and recovery own that boundary.

## CI and scanner availability

The workflow uses read-only permissions and full-SHA action pins. Docker Scout
and Gitleaks are independent gates, while NuGet/npm audits cover application
dependency graphs. If advisory or registry access is unavailable, the result is
inconclusive—not clean. Retry the same pinned command and retain the outage; do
not bypass it for release. R-020 remains open until the workflow runs on a real
GitHub-hosted runner.

CI preserves backend and frontend coverage artifacts. Vue enforces the current
non-regression floors (35% statements, 30% branches, 25% functions, 38% lines)
while generated code and the bootstrap entry point are excluded. These modest
floors do not establish test adequacy; the mandatory risk-specific tenant,
concurrency, idempotency, workflow, accessibility and malformed-contract gates
do. Backend binary coverage is reported without a percentage floor because much
of the highest-value behavior executes through Docker/PostgreSQL/Dapr scripts
outside the unit-test process; replacing those proofs with a line target would
reward the wrong behavior.
