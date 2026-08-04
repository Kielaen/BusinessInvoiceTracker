# ADR-019: Migration deployment and supply-chain gates

- Status: Accepted
- Date: 2026-08-04
- Requirements: REQ-013, REQ-015, REQ-019
- Risks: R-020, R-055, R-057, R-059
- Verification: Phase 11 migration, scan, CI and clean-copy evidence

## Context

The local Compose topology already runs a one-shot `migrate` service before API
replicas, and PostgreSQL tests upgrade several prior snapshots. Phase 11 must
turn that useful local behavior into an explicit deployment decision, detect
model drift, produce reviewable migration output, and prevent the application
replicas from racing to alter schema. The delivery gate must also inspect the
actual final images and repository material rather than treating successful
package restore as complete supply-chain evidence.

## Decision

- Keep migrations out of normal API and automation startup. A separately invoked
  one-shot command owns schema application and must succeed before app services
  start.
- CI generates an idempotent SQL artifact for review, checks for pending model
  changes, and tests upgrade from empty and supported earlier snapshots.
- The local Compose migrator may use the application image and `--migrate` entry
  point for the reviewer journey. A real deployment should execute the reviewed
  artifact or an immutable migration bundle with a deployment identity that has
  schema privileges; steady-state app identities should not.
- Rollback means restore a verified database backup and deploy the last compatible
  application/schema pair. Destructive automatic down-migration is not promised.
- Locked NuGet/npm audits, repository secret scanning, final-image CVE scanning,
  SBOM generation and non-root inspection form separate gates. Findings are
  recorded and triaged; scanners are not described as proof of security.
- CI actions remain full-SHA pinned and least-privilege. Local mirror scripts
  remain runnable without a GitHub account.

## Primary evidence

Current EF Core guidance recommends reviewed SQL scripts for production,
supports idempotent scripts and migration bundles, warns against application
startup migration, and provides a pending-model-change check. EF Core 9 and
later migration APIs take a database-wide lock, but that does not make granting
schema privileges to every replica or mixing deployment with startup the chosen
design. GitHub recommends full-length action SHA pinning. Official .NET Linux
images provide the non-root `app` identity used by the runtime targets.

## Alternatives considered

- Every replica calls `MigrateAsync` at startup: rejected because deployment
  failure, schema privileges and application rollout become coupled even with a
  provider migration lock.
- `EnsureCreated`: rejected because it bypasses reviewed migrations and cannot
  safely evolve retained databases.
- Add a permanent migration microservice: rejected; a finite deployment job is
  sufficient and has lower operational cost.
- Treat dependency audit alone as a container scan: rejected because OS/runtime
  packages in final layers are outside NuGet/npm inventories.
- Automatically fail on every scanner severity regardless of applicability:
  rejected because false positives would encourage suppression; critical/high
  findings instead require explicit triage and evidence.

## Consequences and operational cost

Deployment has an explicit pre-application step and produces reviewable
artifacts. Application availability is not conflated with schema ownership.
Additional CI time is spent generating SQL/SBOMs and scanning final images.
Local Compose remains convenient. An isolated executable rehearsal proves
backup/restore inventory and separate migrator/application permissions without
making Dapr component initialization or the default reviewer path depend on
external role provisioning. Hosting credentials and scheduled backup operation
remain deployment-environment responsibilities.

## Failure modes

A failed migration prevents dependent app containers from starting. A scanner
registry/advisory outage can make the verification inconclusive and must not be
reported as a clean result. A technically valid migration may still lock or
rewrite too much production data; reviewed SQL and representative data are
required, and this assessment cannot prove production-scale duration.

## Reversal or fallback

Use a reviewed idempotent SQL script directly if bundle generation is unreliable.
Run supply-chain tools in an isolated pinned container if a hosted action is
unavailable. No fallback may reintroduce replica-owned migrations, suppress a
finding, or claim that an incomplete scan passed.

## Verification

Accepted by V-1200 through V-1208 and the exact clean-copy gate V-1299. The
one-shot migrator applied the reviewed schema twice, an earlier supported
snapshot upgraded, model drift was zero, final images ran as users 1654/1654/101,
and repository/image/SBOM gates passed. Remote GitHub-hosted execution remains
unobserved under R-020 and is not inferred from the local CI mirror.

V-1209 additionally verifies a custom-format backup/restore with equal stable
inventory, application DML before and after the index-only latest migration, and
separate temporary identities where the migrator can change schema but the
application identity receives an expected permission denial. The exact temporary
objects are removed after the run. This bounds the current deployment pattern;
it does not establish a production SLO or universal zero-downtime migration.
V-1210 repeats that proof from untouched copied source and fresh volumes as part
of every retained Phase 2-11 gate.
