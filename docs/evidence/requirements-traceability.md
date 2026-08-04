# Requirements Traceability

Status values are `Unstarted`, `In progress`, `Proven`, or `Blocked`. A row becomes
`Proven` only when its evidence can be run or inspected in this repository.

| ID | Source requirement | Planned implementation phase | Required evidence | Status |
|---|---|---:|---|---|
| REQ-001 | Public full-stack Business Invoice Tracker | 1–12 | Public-ready repository and final submission rehearsal | In progress: public repository and hosted CI are proven by V-1211/V-1212; final Phase 12 rehearsal remains |
| REQ-002 | Secure sign-in and sign-out | 2 | Auth, logout, CSRF, 401/403, and two-replica session tests | Proven for Phase 2 by V-0302, V-0303, V-0305, V-0306 |
| REQ-003 | Tenant membership and Owner/Accountant/Viewer authorization | 2 | Complete endpoint authorization matrix including cross-tenant IDs | Proven for Phase 2 and repeated for customer endpoints by V-0602: Viewer reads, Owner/Accountant mutate, Viewer mutation is 403, and missing/cross-tenant ownership is 404 |
| REQ-004 | Create, view, edit, search, and paginate customers | 5 | API, PostgreSQL, frontend, accessibility, and E2E tests | Proven by V-0601 through V-0605 and V-0608 |
| REQ-005 | Create/edit draft invoices and line items | 6 | Vertical E2E plus domain/API/frontend tests | Proven by V-0701, V-0702 and V-0704: create/edit/dynamic lines persist through nginx and PostgreSQL |
| REQ-006 | Server-authoritative subtotal, tax, total, paid, and outstanding amounts | 3, 6 | Deterministic domain tests and contract/E2E proof | Proven for drafts by V-0402/3 and V-0701/2/4; clients submit no calculated totals and render persisted aggregate values |
| REQ-007 | Issue, filter, sort, search, paginate, preview, and audit invoices | 6, 7, 9 | Integration and E2E evidence | Proven through Phase 9 by V-0702/4, V-0802/3 and V-1001/2/4; filtering, sorting, search and pagination are bounded and live controls execute |
| REQ-008 | Prevent invalid lifecycle transitions and stale updates | 3, 6, 7 | State-machine tests and genuine concurrent conflict test | Proven through synchronous lifecycle by V-0403/5, V-0702/5 and V-0802/3: invalid/terminal transitions fail and concurrent writers cannot overwrite or overpay |
| REQ-009 | Record idempotent partial/full payments without overpayment | 7 | Duplicate and concurrent PostgreSQL/API tests | Proven by V-0802: exact replay is safe, changed identity reuse conflicts, partial/full transitions pass, overpay is rejected, and a real race commits only once |
| REQ-010 | Durable overdue tracking, cancellation/reschedule, and simulated notification | 8 | Timer/restart/external-event/race/retry/Mailpit tests | Proven by V-0903 through V-0910 and clean-copy V-0912 |
| REQ-011 | Prevent duplicate overdue transitions and successful deliveries | 8 | Duplicate event/inbox/idempotent activity tests | Proven by V-0905, V-0907 through V-0910 and V-0912 |
| REQ-012 | Tenant-scoped bounded dashboard and recent activity | 9 | Query plan/bounds, tenant tests, and frontend E2E | Proven by ADR-017 and V-1001 through V-1004: tenant-safe aggregates, currency separation, fixed 5/10 lists, generated runtime validation and clean-copy execution |
| REQ-013 | Runnable root Dockerfile and complete Compose reviewer journey | 0, 1, 11, 12 | Clean-copy build/start/test/cleanup execution | Proven by V-1206/V-1208/V-1299 and Phase 12 V-1306-R1/V-1308: copied source, fresh volumes, complete gates, comprehensive cleanup and empty-volume restart pass |
| REQ-014 | README with exact build, run, test, health, observability and cleanup commands | 1, 12 | Commands rerun from clean copy and coherent retained topology | Proven by V-1304-R1, V-1306-R1, V-1307-R1 and V-1308-R1; the cleanup omission exposed during rehearsal was corrected and rerun |
| REQ-015 | Production-minded claims must be backed by evidence | All | Verification ledger, ADRs, tests, scans, and honest limitations | Proven through the Phase 12 hostile review and definitive rehearsal by V-1300 through V-1308; production SLO, deployment, backup policy, zero-downtime migration and penetration testing remain explicit non-claims |
| REQ-016 | ASP.NET OpenAPI is the frontend contract source | 0, 4 | Generated artifact drift and malformed-response tests | Proven by V-0501 through V-0503: build-time OpenAPI generates the SDK and Zod schemas, zero drift passes, and malformed success data is visibly rejected |
| REQ-017 | Observability and truthful health behaviour | 0, 1, 10 | Trace/log/metric continuity and dependency-failure tests | Proven by V-1102 through V-1105: dependency/process health, recovery, correlated diagnostics, cached operator inventory and optional viewer independence pass retained and fresh-copy runs |
| REQ-018 | Accessibility and complete frontend state handling | 4–9 | Axe, component, keyboard, and E2E tests | Proven through Phase 9 by V-1002 and V-1005-R1: axe, async states, labels, announcements, functional controls, responsive live behavior and the user-run manual keyboard script pass |
| REQ-019 | PostgreSQL constraints, concurrency, and migrations | 1, 3, 7, 11 | Real PostgreSQL integration and migration tests | Proven by prior PostgreSQL constraints/races plus V-1203/V-1208/V-1209/V-1210: zero model drift, reviewed idempotent SQL, earlier-snapshot upgrade, repeat one-shot migration, clean-copy empty database, backup/restore inventory and separate migration/application roles |
| REQ-020 | Dapr is adopted only if its operational behaviour is proven | 0, 8 | Phase 0 spike or recorded Hangfire fallback | Proven for adoption and product workflow by V-0101-R1/V-0102-R1 and V-0906 through V-0913 |

## Source notes

The original assessment brief is intentionally concise: build a full-stack Business
Invoice Tracker with simulated overdue notifications, a runnable Dockerfile, and a
README explaining build/run/test. The governing plan expands those requirements
without weakening them.
