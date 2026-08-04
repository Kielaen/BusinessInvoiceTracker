# Phase 1 Working Plan

Status: **GATE PASSED**  
Started: 2026-07-31 (Africa/Johannesburg)  
Previous gate: Phase 0 passed as V-0199-R1.

## Objective

Prove one authenticated request through the real Vue, nginx, ASP.NET Core,
PostgreSQL, outbox/worker, Dapr, and telemetry path before adding invoice domain
breadth.

## Governing rules

All constitution rules remain binding. The most direct rules for this phase are
SE3-01, SE3-04, SE3-05, SE3-09, SE3-10, SE3-14, SE3-16, SE3-21 through SE3-25,
SE3-28 through SE3-31, and SE3-34.

## Work sequence

Only Phase 1 is active:

1. Inventory the accepted Phase 0 spike assets and define what graduates into the
   product versus what remains evidence-only.
2. Create the .NET solution with modular API, persistence, and automation worker
   projects plus warning-as-error builds and locked dependencies.
3. Create the Vue application and retain the proven generated OpenAPI/Zod
   contract boundary.
4. Add the initial PostgreSQL migration pipeline and a trivial authenticated
   vertical record owned by the API.
5. Add the automation worker, Dapr sidecar/control plane, and trace propagation
   without adding invoice workflows yet.
6. Add nginx same-origin routing, truthful live/ready probes, non-root application
   containers, root Dockerfile, Compose, `.env.example`, and safe startup order.
7. Add initial CI for backend, frontend, tests, dependency checks, and container
   builds.
8. Execute the walking-skeleton request and prove it is traceable across relevant
   components.
9. Run the Phase 1 clean-clone gate and reconcile evidence, risks, traceability,
   ADR consequences, and reviewer commands.

## Gate

- A clean clone builds and starts.
- API, web, PostgreSQL, Dapr, and the worker become healthy.
- One authenticated request traverses the intended stack and is traceable.
- Application containers run non-root where applicable.
- CI builds backend, frontend, and container images.

## Scope guard

Phase 1 does not add customer, invoice, payment, overdue, dashboard, or audit
features. Identity remains the smallest walking-skeleton implementation needed to
exercise the accepted cookie/antiforgery contract; full tenancy and authorization
belong to Phase 2.

## Gate result

Passed on 2026-07-31 as V-0299. The clean-copy run built every image, applied
migrations, reached healthy API/PostgreSQL/worker/Dapr state, completed the
authenticated Vue/nginx/API/outbox/Dapr/worker path, rejected anonymous access,
proved duplicate delivery harmless, and verified non-root application users.
The optional profile separately delivered logs, traces, and metrics for both
.NET services. The initial CI workflow contains the same locally passing locked
build/test/scan/image-build commands; no remote GitHub runner has been invoked
because the repository has no configured remote.

Next phase: Phase 2, with no Phase 2 implementation started in this document.
