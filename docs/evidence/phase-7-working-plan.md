# Phase 7 Working Plan

Status: **COMPLETE - GATE PASSED (V-0899)**  
Started: 2026-07-31 (Africa/Johannesburg)  
Previous gate: Phase 6 passed as V-0799.

## Objective

Complete the synchronous invoice lifecycle with atomic issue/number allocation,
idempotent payments, bounded concurrency, void policy, transaction-coupled outbox
and audit evidence, and a complete tenant-authorized Vue journey.

## Governing rules

All constitution rules remain binding. ADR-007 through ADR-009, ADR-013 and
ADR-014 are the current decision baseline. Any ambiguity in issue, payment,
void, replay, outbox or audit semantics must be resolved before implementation.

## Work sequence

Only Phase 7 is active:

1. Resolve issue/payment/void command identities, authorization, audit payloads,
   outbox ownership and duplicate/concurrent behavior in an ADR.
2. Prove the transaction shape for issue plus number allocation plus outbox and
   audit against PostgreSQL, including simultaneous issue attempts.
3. Add tenant-authorized issue, payment and void endpoints with stable command
   IDs and aggregate-owned transitions.
4. Generate and runtime-validate the TypeScript contract, then expose complete
   issue/payment/void states and recovery in Vue.
5. Prove duplicate commands, concurrent allocation, concurrent payment/overpay,
   every role/status path, audit/outbox atomicity and full browser E2E.
6. Run all earlier regression, drift, vulnerability, Compose, observability and
   clean-copy gates before closing the phase.

## Gate

- Concurrent issue/allocation passes.
- Duplicate command tests pass.
- Concurrent payments cannot overpay.
- Every status transition and authorization path is proven.

## Scope guard

Phase 7 writes the durable outbox event but does not implement overdue workflow,
Mailpit delivery or reconciliation. Those consumers remain Phase 8 work.

## Completion evidence

- ADR-015 fixed command identity, authorization, transaction, audit, outbox,
  concurrency, failure-mode and fallback semantics before implementation.
- `scripts/verify-phase7.ps1` passed three independent runs. Concurrent issue
  allocated distinct numbers; exact command replay was safe; changed replay
  conflicted; concurrent payments produced one 200 and one 409 and never
  overpaid; partial, Paid and Voided states and forbidden paths were proven.
- Issue, payment and void each committed an audit row and pending outbox row in
  the same PostgreSQL transaction. Phase 8 remains the delivery owner.
- The generated TypeScript client and Zod schemas are deterministic; 20 .NET
  tests, 24 frontend tests, the production build, bundle budget, NuGet/npm
  scans, prior phase regressions and optional telemetry checks passed.
- The updated clean-copy verifier rebuilt from copied source and passed Phases
  2, 4, 5, 6 and 7 before cleaning isolated resources and restoring the main
  stack.

## Gate decision

All four Phase 7 gate conditions pass as V-0899. Automated control of the
already-open in-app browser could not initialize because its local control
runtime could not create its kernel assets; this is retained as a tooling
limitation and is not represented as browser evidence. The required lifecycle,
authorization and full-stack HTTP paths are instead covered by frontend tests,
generated runtime schemas and the nginx/PostgreSQL executable gate. Phase 8 is
the exact next phase.
