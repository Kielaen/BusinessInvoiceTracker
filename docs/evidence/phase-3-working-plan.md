# Phase 3 Working Plan

Status: **GATE PASSED**  
Started: 2026-07-31 (Africa/Johannesburg)  
Previous gate: Phase 2 passed as V-0399.

## Objective

Implement the financial domain and PostgreSQL persistence foundation while
proving deterministic calculations, lifecycle rejection, atomic invoice-number
allocation, optimistic concurrency, and forward migration from both empty and
Phase 2 databases.

## Governing rules

All constitution rules remain binding. The most direct rules for this phase are
SE3-01, SE3-04 through SE3-10, SE3-12, SE3-15 through SE3-17, SE3-23 through
SE3-25, SE3-27 through SE3-30, SE3-32, and SE3-34.

## Work sequence

Only Phase 3 is active:

1. Record ADR-007 invoice-number semantics, ADR-008 money/tax/rounding, and
   ADR-009 lifecycle/edit rules before encoding them.
2. Add domain rule errors and value objects for currency, money, tax rate,
   invoice number, and invoice line inputs.
3. Implement the Invoice aggregate with server-derived totals, explicit
   transitions, positive/bounded payment rules, due-instant semantics, and a
   mutation-owned concurrency version.
4. Add tenant-owned Customer, Invoice, InvoiceLine, Payment, AuditEntry, and
   InvoiceNumberSequence persistence with database constraints and indexes.
5. Implement the allocator as one PostgreSQL `INSERT ... ON CONFLICT DO UPDATE
   ... RETURNING` statement; promise uniqueness and monotonic allocation per
   business, never gaplessness.
6. Add exact-pinned unit and PostgreSQL integration test projects. Use real
   PostgreSQL for migrations, constraints, concurrency, and allocation.
7. Generate the migration and prove it applies from an empty database and a
   database stopped at the Phase 2 migration.
8. Rerun locked builds, dependency scans, Phase 1/2 regressions, and clean-copy
   verification; reconcile evidence, risks, traceability, ADRs, and commands.

## Gate

- A genuinely concurrent PostgreSQL allocation test proves the documented
  uniqueness and monotonic semantics.
- Financial calculations are deterministic across boundary and midpoint cases.
- Every invalid lifecycle transition in ADR-009 is rejected centrally.
- The migration applies from an empty database and from the Phase 2 schema
  snapshot.

## Scope guard

Phase 3 establishes domain and persistence behavior but does not expose customer,
invoice, or payment product endpoints. Those vertical slices remain Phases 5
through 7. Dapr overdue orchestration remains Phase 8.

## Completed result

ADR-007 through ADR-009 were accepted before implementation. The repository now
contains explicit financial value objects, a bounded invoice aggregate, persisted
Customer/Invoice/Line/Payment/Audit/Sequence models, tenant-bearing foreign keys,
database checks, GUID optimistic concurrency, and a single-statement PostgreSQL
allocator.

## Phase report

- Completed: deterministic calculation policy; lifecycle/edit rules; due-instant
  implementation; tenant-owned relational schema; atomic allocation; optimistic
  concurrency; exact-pinned xUnit/Testcontainers suite; migration and regression
  verification.
- Constitution applied: SE3-01 evidence over claims; SE3-04 failure analysis;
  SE3-06 PostgreSQL authority; SE3-07 invalid-state resistance; SE3-08 genuine
  concurrency; SE3-10 visible consistency boundaries; SE3-11 bounded lines;
  SE3-12/15 tenant and exposure defense; SE3-27 through SE3-30 risk-shaped real
  infrastructure and clean-copy proof; SE3-32 migration evolution; SE3-34
  executable documentation.
- Evidence passed: V-0400 through V-0409.
- Decisions: ADR-007 numbering, ADR-008 financial calculation, ADR-009 lifecycle,
  and existing ADR-014 due-instant rules.
- Remaining risks: API-level 409 handling and idempotent payment commands remain
  Phases 6/7; audit/outbox atomic command proof and workflow races remain
  Phases 7/8; remote CI remains R-020.
- Gate: passed as V-0499.
- Next: Phase 4, contract and frontend foundation.

No Phase 4 implementation began before this gate passed.
