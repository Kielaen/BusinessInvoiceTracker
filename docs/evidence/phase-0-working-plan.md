# Phase 0 Working Plan

Status: **PASSED**  
Started: 2026-07-30 (Africa/Johannesburg)  
Completed: 2026-07-31 (Africa/Johannesburg)  
Next phase: **Phase 1 — Walking skeleton**.

The original Docker blocker was resolved after Docker Desktop installation and
firmware virtualization enablement. Historical blocked evidence remains in the
verification ledger; all mandatory reruns now pass.

## Objective

Resolve architecture-affecting uncertainty with primary documentation and executable
spikes before building product breadth.

## Governing rules

The most directly applicable constitution rules are SE3-01, SE3-04, SE3-05,
SE3-09, SE3-10, SE3-14, SE3-16, SE3-21 through SE3-25, SE3-28 through
SE3-31, and SE3-34. All constitution rules remain binding.

## Work sequence

Only Phase 0 is active. Within it, work proceeds in these reviewable increments:

1. Establish the repository and preserve the governing documents.
2. Create the evidence ledger, traceability table, ADR structure, and risk register.
3. Verify current versions and behavioural claims using primary sources.
4. Pin a candidate .NET, Dapr runtime/SDK, container, npm, and tooling set.
5. Build a minimal Dapr Workflow spike that proves timer durability, restart
   recovery, and external-event behaviour.
6. Build a Compose topology spike and execute it from a clean working copy.
7. Prove cookie authentication, API 401/403 behaviour, antiforgery rejection,
   nginx same-origin routing, and shared data-protection keys.
8. Prove OpenAPI-to-TypeScript/runtime-schema generation and malformed-response
   rejection.
9. Prove an always-on low-fragility telemetry baseline and an optional local viewer.
10. Write ADR-001 through ADR-005, ADR-010, ADR-012, and ADR-014.
11. Run the exact Phase 0 gate and update every evidence/risk entry.

## Phase 0 acceptance evidence

- Every baseline uncertainty has executable evidence or the documented fallback.
- `docker compose up --build` succeeds for the pinned spike topology.
- Timer, restart recovery, and external-event tests pass against the pinned Dapr
  runtime and .NET SDK.
- Cookie/CSRF/nginx and generated-schema tests pass.
- Critical dependencies and images are pinned.
- The selected architecture is no broader than the proposed modular monolith plus
  one automation worker.
- Verification commands, failures, limitations, and decisions are recorded
  truthfully in `verification.md`.

## Starting constraint and resolution

At discovery time the host had .NET SDK 9.0.101 and Node.js 24.14.1/npm 11.11.0.
Docker and the Dapr CLI were not available on `PATH`. Docker Desktop 4.84.0,
Engine 29.6.2 and Compose v5.3.1 are now operational. A standalone Dapr CLI is not
required because the runtime and control-plane binaries are exact-pinned
containers.

## Progress snapshot

- Steps 1-11 are complete.
- The exact-pinned topology builds, starts, verifies and cleans up from a guarded
  clean copy.
- Dapr timer recovery and buffered external events pass after worker and sidecar
  restart using PostgreSQL v2 actor state.
- nginx cookie/antiforgery integration and the generated Zod boundary pass.
- Default structured telemetry has no viewer dependency. The optional Aspire
  profile received logs, traces and metrics from both .NET services.
- V-0199-R1 records the Phase 0 gate as PASS.
