# Phase 6 Working Plan

Status: **GATE PASSED (V-0799)**  
Started: 2026-07-31 (Africa/Johannesburg)  
Previous gate: Phase 5 passed as V-0699.

## Objective

Deliver draft invoice creation and editing with server-authoritative financial
calculations, optimistic concurrency, audit evidence, preview, and a complete
tenant-scoped Vue journey.

## Governing rules

All constitution rules remain binding. ADR-006 through ADR-010, ADR-013 and
ADR-014 govern tenancy, numbering, money, lifecycle, contracts, draft commands,
and due-time semantics.

## Work sequence

Only Phase 6 is active:

1. Resolve draft command, numbering, replay, preview and audit semantics before
   exposing endpoints.
2. Add tenant-authorized draft list/create/detail/edit and audit endpoints using
   the existing domain aggregate as the only totals authority.
3. Generate and runtime-validate the TypeScript contract.
4. Build accessible invoice list/form/preview views with dynamic line editing,
   complete states, and visible stale-write recovery.
5. Prove totals, tenant isolation, replay, concurrency, audit, PostgreSQL
   persistence, accessibility and E2E behavior.
6. Run every earlier regression, drift, vulnerability, Compose and clean-copy
   gate before closing the phase.

## Gate

- The server is authoritative for all displayed totals.
- A stale concurrent edit returns and displays 409.
- Line and total boundary cases pass.
- The complete draft workflow is proven through nginx and PostgreSQL.

## Scope guard

Phase 6 does not issue, number, void, pay, send, or schedule invoices. Those
state transitions remain Phase 7 and Phase 8 work.

## Completion report

- Completed: tenant-authorized draft list/create/detail/edit/audit API; dynamic
  Vue form, preview and audit views; generated runtime-validated client; server
  totals; stable replay; optimistic conflict recovery; structured telemetry.
- Constitution rules applied: server/domain authority, tenant-safe lookup,
  antiforgery, bounded input/query sizes, explicit idempotency/concurrency,
  transactional audit, primary contract generation, no hidden failures, and
  clean-copy proof before claims.
- Evidence passed: V-0700 through V-0708, including 20 .NET tests, 23 frontend
  tests, two API gate runs, two-browser stale conflict, optional telemetry, all
  earlier regressions and isolated empty-database Compose execution.
- Decisions: ADR-013 accepted; ADR-007 amended so number allocation remains the
  Phase 7 issue boundary.
- Remaining risks: remote CI is still unobserved (R-020); Phase 7 must prove
  concurrent number allocation at the issue API, payment idempotency and outbox
  atomicity. Audit retention policy remains outside the assessment scope.
- Gate: **PASSED** as V-0799.
- Next: Phase 7 — issue and payment lifecycle.
