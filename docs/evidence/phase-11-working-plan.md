# Phase 11 Working Plan

Status: **PASSED (V-1299)**  
Started: 2026-08-04 (Africa/Johannesburg)  
Previous gate: Phase 10 passed as V-1199.

## Objective

Remove production-trust gaps with executable security, supply-chain, migration,
performance, shutdown and clean-delivery evidence. This phase hardens the
existing product; it does not add product scope or optional infrastructure.

## Governing rules

The constitution and implementation plan remain binding. Findings are triaged,
not hidden. Application replicas never migrate on startup. Security tests cover
authorization and abuse through the real same-origin boundary. Load evidence is
a representative local profile, not an SLO or scalability claim. Shutdown proof
must inspect durable state after restart. New dependencies require a concrete
risk benefit and exact pinning.

## Evidence design

| Concern | Requirement/risk | Executable proof | Failure consequence |
|---|---|---|---|
| Threat model and abuse | REQ-002/3/15; R-054 | Endpoint/data-flow review plus anonymous, role, tenant, CSRF, cookie/header, method and rate-limit tests | Phase remains blocked for exploitable authorization or untriaged high finding |
| Dependencies and secrets | REQ-013/15; R-055 | Locked restores, NuGet/npm audit, repository secret scan, image CVE scan and SBOM | Critical/high finding is fixed, explicitly non-applicable with evidence, or blocks gate |
| Containers | REQ-013; R-055 | Build all final targets, inspect user, health and immutable runtime inputs | Root application image or unscanned final image blocks gate |
| Representative performance | REQ-007/11/12; R-056 | Bounded concurrent authenticated reads and existing write-race tests; record latency distribution and errors | Fix query/resource issue or document measured limitation without scalability claim |
| Migration deployment | REQ-019; R-057 | Pending-model check, reviewed idempotent SQL, empty and prior snapshot upgrade, one-shot migrator, repeat and app-start ordering | App replicas must not start against an unproven schema |
| Graceful shutdown | REQ-010/11/17; R-058 | Stop publisher/automation during durable work, inspect attempts/cardinality, restart and await recovery | Fix cancellation/durability or retain Phase 10 recovery fallback and block gate |
| Clean delivery and CI | REQ-013/14/15; R-020/R-059 | CI mirrors local gates; clean copied source/fresh volumes runs build, scans, migration, Phase 2-11 checks and cleanup | Broken or undocumented reviewer command blocks gate |

## Work sequence

Only Phase 11 is active:

1. Record current primary-source guidance, ADR-019 and risks before changing
   migration or delivery policy.
2. Add a focused threat model and executable OWASP-oriented abuse assertions by
   extending existing real-stack security evidence instead of introducing a
   second test framework.
3. Add deterministic repository/container supply-chain checks, SBOM generation,
   non-root assertions and CI enforcement with pinned actions/tools.
4. Generate and inspect migration artifacts; prove empty, prior snapshot,
   repeat-safe and one-shot Compose deployment behavior without replica races.
5. Run a bounded authenticated read profile and preserve the existing genuine
   concurrency tests for writes.
6. Prove graceful stop/restart behavior for asynchronous work, then run the full
   locked regression and isolated clean-copy gate.
7. Reconcile the ADR, threat model, runbook, requirements, risks and evidence.

## Gate

- No critical/high untriaged security findings.
- No hardcoded secrets.
- No known broken build/run/test instructions.
- Performance findings are resolved or honestly documented.
- Graceful shutdown preserves or safely retries asynchronous work.

## Scope guard

Phase 11 will not claim penetration testing, certified compliance, a production
SLO, internet-scale load, zero vulnerabilities, or zero-downtime migration. A
local scanner result covers only the scanned artifact and advisory snapshot.
Remote GitHub execution remains distinct from locally mirrored CI evidence.

## Completion

The exact focused suite and the untouched isolated clean-copy gate passed on
2026-08-04. Security boundaries, migration deployment, representative local
latency, graceful stop/recovery, repository secret scanning, final-image
critical/high scanning, SBOM generation, non-root identities and CI policy all
have repository evidence. The old nginx base was replaced after its scan found
critical/high packages; the replacement final image had no critical/high finding
in the scanner snapshot.

GitHub-hosted run `30941001930` passed the complete workflow on the published
`main` commit `7a82726`, including committed-history secret scanning, locked
backend/frontend gates and coverage artifacts, three image builds, critical/high
Trivy scans, SPDX SBOM artifacts and the clean Compose reviewer path. The current
full-SHA actions use Node 24 and the run completed without annotations. R-020 and
the remote portion of R-059 are closed as V-1211/V-1212. Production backup
execution, production secret provisioning, capacity/SLO validation and universal
zero-downtime schema evolution remain deployment responsibilities, not claims of
this assessment gate. Phase 12 is the exact next phase.

Pre-Phase-12 extension V-1209 subsequently executed the locally provable parts:
a backup was restored into an isolated database with equal stable inventory, and
separate temporary migrator/application roles proved schema ownership versus DML
and explicit schema denial. The current index-only migration accepted application
DML before and after application. Default Compose still uses clearly local-only
reviewer credentials; hosting secret provisioning, production-scale duration/SLO
and a universal zero-downtime claim remain outside this evidence.

The expanded suite then passed from untouched copy
`business-invoice-phase2-clean-827baaad38734482b156dd08cf5db249` as V-1210 and
V-1299-R1. Its isolated containers, volumes, temporary databases/roles and copied
source were removed, and the retained main stack was restored.

The public-repository and hosted-runner extension then passed as V-1211, V-1212
and V-1299-R2. The failed entitlement run and the successful fallback/remediation
runs remain visible; no failed evidence was erased or relabelled.
