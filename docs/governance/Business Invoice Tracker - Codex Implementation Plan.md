# Business Invoice Tracker — Codex Implementation Plan

## 1. Mission

Build and publicly submit a production-minded Full-Stack Business Invoice Tracker that demonstrates Software Engineer III capability across architecture, backend, frontend, integration, security, resilience, observability, testing, delivery, and technical judgment.

The application must allow businesses to:

- Sign in securely.
- Manage customers.
- Create and edit draft invoices with line items.
- Issue and track invoices.
- Record payments.
- Identify overdue invoices.
- Receive simulated overdue notifications.
- Review invoice history and audit activity.

The solution will use:

- .NET 10 and ASP.NET Core.
- Vue 3, TypeScript, Vite, and Composition API.
- PostgreSQL and EF Core.
- Dapr Workflow and Pub/Sub only where they solve durable overdue-processing concerns.
- Docker and Docker Compose for the complete reviewer journey.

The project is governed by the **SE3 Engineering Constitution**. Codex must read it before every new implementation phase and reference its rule IDs in plans, reviews, and exception decisions.

Related governing document:

- `SE3 Engineering Constitution.md`

## 2. Non-negotiable operating rule

> Do not implement an assumption as architecture when reasonable doubt remains. Resolve the doubt with primary documentation, a focused spike, an executable test, or a recorded ADR. If it cannot be resolved safely within the timebox, choose the simpler proven fallback.

This rule applies to framework behaviour, Dapr semantics, security, financial calculations, concurrency, deployment, testing tools, generated contracts, and infrastructure.

Codex must never:

- Add a technology only to make the project look sophisticated.
- describe unverified behaviour as production-grade.
- Hide uncertainty behind comments, TODOs, or documentation.
- Leave a failing test, warning, security finding, or build issue unexplained.
- suppress an error merely to make a quality gate pass.
- proceed past a phase gate with material unresolved risk.
- trade required full-stack completion for optional infrastructure.

## 3. Codex execution protocol

At the start of every phase, Codex must:

1. Read the SE3 Engineering Constitution.
2. Read this plan and the latest evidence ledger.
3. Inspect repository guidance such as `AGENTS.md`, if present.
4. Check the worktree and preserve unrelated user changes.
5. State the phase objective, applicable constitution rules, risks, and acceptance evidence.
6. Research any unstable or uncertain technical fact using primary sources.
7. Create or update an ADR before making a material architectural choice.
8. Implement the smallest complete vertical increment.
9. Run proportionate verification.
10. Update the evidence ledger before declaring the phase complete.

Codex must maintain:

- `docs/architecture/`
- `docs/adr/`
- `docs/evidence/verification.md`
- `docs/evidence/requirements-traceability.md`
- `docs/evidence/risk-register.md`
- `docs/runbooks/`

No phase is complete because code was written. It is complete only when its acceptance evidence exists and passes.

## 4. Decision hierarchy

When priorities conflict, Codex must protect work in this order:

1. Complete required end-to-end user journeys.
2. Security and tenant isolation.
3. Financial and domain correctness.
4. Concurrency and idempotency.
5. Full-stack contract correctness.
6. Failure handling and operability.
7. Risk-based automated tests.
8. Accessibility and user experience.
9. Clean-clone delivery and truthful documentation.
10. Optional infrastructure and showcase features.

Items 1–9 may not be sacrificed to preserve item 10.

## 5. Scope

### 5.1 Required product scope

#### Authentication and tenancy

- Secure sign-in and sign-out.
- One seeded demonstration business with multiple roles.
- Business membership and resource-level authorization.
- Roles:
  - Owner: complete access, including membership-sensitive operations.
  - Accountant: manage customers, invoices, and payments.
  - Viewer: read-only access.
- Cross-tenant access must be prevented and tested.

#### Customers

- Create, view, edit, search, and paginate customers.
- Prevent unsafe deletion where invoices reference a customer.
- Validate names, contact details, and billing information.

#### Invoices

- Create and edit drafts.
- Add, edit, and remove line items.
- Calculate subtotal, tax, total, paid amount, and outstanding amount server-side.
- Preview invoice details.
- Issue invoices.
- Filter, sort, search, and paginate invoice lists.
- View invoice history and audit events.
- Prevent invalid lifecycle transitions.
- Prevent stale concurrent updates.

#### Payments

- Record full or partial payments.
- Prevent payments exceeding the outstanding amount.
- Make payment recording idempotent.
- Transition invoices to partially paid or paid atomically.

#### Overdue automation

- Start durable tracking when an invoice is issued.
- Wait until the due instant.
- Cancel or alter tracking when an invoice is paid, voided, or its due date changes.
- Mark an eligible unpaid invoice overdue.
- Send a simulated notification through Mailpit.
- Record each notification attempt and outcome.
- Retry transient notification failures safely and finitely.
- Prevent duplicate overdue transitions and duplicate successful delivery records.

#### Dashboard

- Show server-calculated summary metrics.
- Show recent activity and overdue invoices.
- Ensure all summary data is tenant-scoped.
- Avoid loading unbounded invoice data into the browser or application process.

### 5.2 Explicitly out of scope unless all submission gates already pass

- Real payment processing.
- Real accounting, tax filing, or legal compliance claims.
- PDF generation.
- Multi-currency conversion.
- Bank integrations.
- Public customer payment portal.
- Kubernetes or Helm.
- Micro-frontends.
- More than the justified API and automation-worker deployables.
- AI features.
- Native mobile applications.

These may be documented as future extensions. They must not be implemented ahead of required hardening.

## 6. Baseline architecture

### 6.1 Architectural style

Use a modular monolith for synchronous business capabilities plus one independently deployable automation worker.

Deployables:

1. `Invoice.Api`
   - Authentication.
   - Customer, invoice, payment, dashboard, audit, and internal automation APIs.
   - EF Core transactional work.
   - Transactional outbox publisher.

2. `Invoice.Automation`
   - Dapr Pub/Sub subscribers.
   - Dapr Workflow orchestration and activities.
   - Overdue evaluation and simulated notification delivery.
   - Inbox/idempotency processing.

3. `Invoice.Web`
   - Vue application.
   - Served by nginx.
   - Same-origin reverse proxy for `/api`.

Local infrastructure:

- PostgreSQL for business state.
- Redis for Dapr local Pub/Sub and workflow state.
- Mailpit for simulated email.
- Dapr sidecars.
- Dapr placement and scheduler services required by Workflow.
- Optional observability profile after the observability ADR is proven.

### 6.2 Proposed repository structure

```text
/
├── src/
│   ├── Invoice.Domain/
│   ├── Invoice.Application/
│   ├── Invoice.Infrastructure/
│   ├── Invoice.Api/
│   └── Invoice.Automation/
├── web/
│   └── invoice-web/
├── tests/
│   ├── Invoice.Domain.Tests/
│   ├── Invoice.Application.Tests/
│   ├── Invoice.Infrastructure.IntegrationTests/
│   ├── Invoice.Api.IntegrationTests/
│   ├── Invoice.Automation.IntegrationTests/
│   ├── Invoice.Architecture.Tests/
│   └── e2e/
├── deploy/
│   ├── dapr/
│   │   ├── components/
│   │   └── configuration/
│   └── observability/
├── docs/
│   ├── adr/
│   ├── architecture/
│   ├── evidence/
│   └── runbooks/
├── scripts/
├── .github/workflows/
├── Dockerfile
├── compose.yaml
├── .env.example
├── Directory.Build.props
├── Directory.Packages.props
└── README.md
```

The exact structure must be validated during Phase 0. A change requires an ADR only if it materially changes boundaries or deployment.

## 7. Technical baseline and verification status

### 7.1 Accepted baseline

- .NET 10 LTS.
- ASP.NET Core 10.
- EF Core 10 with Npgsql.
- Vue 3 with TypeScript strict mode.
- Vite.
- PostgreSQL.
- Docker multi-stage builds.
- Docker Compose.
- RFC Problem Details.
- OpenTelemetry instrumentation.
- A controllable `TimeProvider` for time-based behaviour.

### 7.2 Baseline requiring a spike before final adoption

#### Dapr version combination

Candidate:

- Dapr runtime `1.17.x`.
- Matching compatible Dapr .NET SDK versions.

Proof required:

- Official compatibility evidence.
- Workflow schedules, survives process restart, receives external events, and resumes.
- Compose environment works from a clean clone.
- Version is pinned everywhere.
- An upgrade/versioning note exists.

Fallback:

- Hangfire with PostgreSQL-backed durable jobs, retaining the EF Core outbox and idempotency design.

#### OpenAPI TypeScript and runtime-schema generation

Candidate:

- `@hey-api/openapi-ts` generation of TypeScript SDK/types and Zod schemas.

Proof required:

- Correct generation from the ASP.NET OpenAPI document.
- Runtime rejection of a deliberately malformed response.
- Support for Problem Details, enums, nullable values, pagination, dates, and decimal-as-string decisions.
- Reproducible generation in CI.
- No hand-edited generated files.

Fallback:

- Kiota-generated TypeScript client plus explicitly maintained Zod schemas for critical boundaries, with contract tests detecting drift.

#### Browser authentication

Candidate:

- ASP.NET Core Identity.
- Secure HTTP-only same-site cookie.
- Same-origin nginx proxy.
- Antiforgery token/header for state-changing requests.
- Persisted data-protection keys for multi-replica correctness.

Proof required:

- 401 and 403 API behaviour, not HTML redirects.
- CSRF rejection test.
- Cross-tenant authorization tests.
- Session survives routing between two API replicas when key persistence is configured.
- Logout invalidates the session as designed.

Fallback:

- Standards-based OIDC Authorization Code with PKCE using a proven local identity provider. Do not implement custom JWT issuance.

#### Observability stack

Candidate:

- Structured JSON logs to stdout.
- OpenTelemetry traces and metrics.
- OTLP exporter.
- A lightweight reviewer-visible local backend selected by ADR.

Proof required:

- A browser request can be followed through API, outbox, Pub/Sub, workflow, and notification activity.
- Metrics show request latency, workflow activity, notification attempts, and failures.
- Stack does not make the default reviewer journey fragile.

Fallback:

- Always-on JSON logs and OTLP instrumentation, with the viewer stack moved to an optional Compose profile.

## 8. Domain model

### 8.1 Core entities and value objects

Proposed entities:

- `Business`
- `ApplicationUser`
- `BusinessMembership`
- `Customer`
- `Invoice`
- `InvoiceLine`
- `Payment`
- `NotificationDelivery`
- `AuditEntry`
- `InvoiceNumberSequence`
- `OutboxMessage`
- `InboxMessage`

Proposed value objects:

- `Money`
- `CurrencyCode`
- `InvoiceNumber`
- `EmailAddress`
- `TaxRate`
- `DateRange` or explicit issue/due date types

### 8.2 Invoice lifecycle

Initial candidate state model:

```text
Draft
  ├── issue ──> Issued
  └── void ───> Voided

Issued
  ├── partial payment ──> PartiallyPaid
  ├── full payment ─────> Paid
  ├── due date passes ──> Overdue
  └── void ─────────────> Voided

PartiallyPaid
  ├── remaining payment ─> Paid
  ├── due date passes ───> Overdue
  └── void ──────────────> Voided, subject to documented business rule

Overdue
  ├── partial payment ───> Overdue
  ├── remaining payment ─> Paid
  └── void ──────────────> Voided, subject to documented business rule
```

Questions that must be resolved in the domain ADR:

- Can issued invoices be edited, or must they be superseded/voided?
- Can an invoice with payments be voided?
- Does partial payment after the due date remain overdue?
- Is the due date interpreted at start or end of the business day?
- Which business time zone determines the due instant?
- What tax and rounding rules are in scope?
- Is invoice numbering gapless, monotonic, or only unique and sequential at allocation?

No code may hardcode answers before these questions are recorded and justified.

### 8.3 Mandatory invariants

- Every tenant-owned row has an unambiguous business owner.
- Every query and command is tenant-scoped.
- Invoice number is unique within a business.
- Invoice number is allocated atomically.
- Invoice totals are derived from validated line items.
- Monetary values cannot be negative unless a specific credit-note feature exists.
- Payment amount must be positive and may not exceed outstanding balance.
- Issuance requires a customer, at least one valid line, and a valid due date.
- Lifecycle transitions occur through domain methods.
- Audit entries accompany material domain changes.
- Outbox events are persisted in the same transaction as the domain change.
- Optimistic concurrency prevents silent lost updates.

## 9. Event and workflow design

### 9.1 Candidate event catalogue

- `InvoiceIssued`
- `InvoiceDueDateChanged`
- `InvoicePaymentRecorded`
- `InvoicePaid`
- `InvoiceVoided`
- `InvoiceBecameOverdue`
- `OverdueNotificationRequested`
- `OverdueNotificationDelivered`
- `OverdueNotificationFailed`

Every event must define:

- Event ID.
- Event type and schema version.
- Occurred-at timestamp.
- Trace/correlation ID.
- Business ID.
- Aggregate ID.
- Causation ID where applicable.
- Minimal payload without unnecessary personal information.

### 9.2 Outbox flow

1. API command validates authentication, authorization, business rules, and concurrency.
2. Domain changes, audit entry, and outbox message are persisted in one PostgreSQL transaction.
3. Outbox publisher claims records using safe multi-replica locking, such as `FOR UPDATE SKIP LOCKED`.
4. Publisher sends the event through Dapr Pub/Sub.
5. Publish success marks the outbox record processed.
6. Failure records attempt information and retries with bounded policy.
7. Poison records become visible for operator action rather than retrying forever.

Required proof:

- API crash before commit emits nothing.
- Crash after commit but before publish is recovered.
- Crash after publish but before marking processed produces a duplicate that the consumer handles safely.
- Two publishers do not corrupt processing.

### 9.3 Overdue workflow candidate

For each issued invoice:

1. Start a workflow with deterministic identity derived from the invoice ID and workflow generation/version.
2. Wait using a durable timer until the due instant.
3. In parallel, accept external events:
   - Paid.
   - Voided.
   - Due date changed.
4. If paid or voided, end without notification.
5. If the due date changes, safely reschedule or continue in a versioned workflow path.
6. At the due instant, call an idempotent activity that re-reads authoritative invoice state.
7. If still eligible, atomically transition to overdue and persist audit/outbox evidence.
8. Execute simulated notification delivery.
9. Retry only transient delivery failures with bounded durable retry.
10. Persist terminal failure for visibility and manual retry.

Required proof:

- Worker restart during timer.
- Worker restart during notification.
- Duplicate `InvoiceIssued`.
- `InvoicePaid` arriving before workflow begins waiting.
- `InvoicePaid` racing with the due timer.
- Due-date change while waiting.
- Notification provider returns transient failure, then succeeds.
- Permanent invalid-address failure does not retry forever.

## 10. Security model

### 10.1 Threat model

Create a lightweight threat model before feature development using:

- Assets.
- Actors and trust boundaries.
- Entry points.
- Tenant isolation risks.
- Authentication and session risks.
- CSRF and XSS.
- Broken object and function-level authorization.
- Mass assignment and excessive data exposure.
- Resource exhaustion.
- Secret leakage.
- Event spoofing and replay.
- Logging of sensitive data.

### 10.2 Mandatory security controls

- Standard password hashing through ASP.NET Core Identity.
- Secure cookie settings.
- Antiforgery protection.
- Policy and resource-based authorization.
- DTO allowlists for input and output.
- Rate limiting for login and costly commands.
- Security headers and Content Security Policy.
- Strict CORS or no cross-origin requirement under same-origin deployment.
- No production secret defaults.
- Environment validation at startup.
- Repository and image secret scanning.
- Audit events for important security and business actions.
- Safe error responses with no stack traces or database details.

### 10.3 Required authorization matrix

Create and test a matrix for every endpoint covering:

- Anonymous.
- Authenticated without membership.
- Viewer in correct tenant.
- Accountant in correct tenant.
- Owner in correct tenant.
- Correct role in a different tenant.
- Disabled or removed membership where applicable.

## 11. API design

### 11.1 API standards

- Versioned route prefix such as `/api/v1`.
- Resource-oriented endpoints.
- RFC Problem Details for all error responses.
- Pagination metadata.
- Server-side filtering and sorting.
- Cancellation-token propagation.
- Idempotency key support on selected commands.
- Optimistic concurrency through ETag/If-Match or an explicit version token.
- OpenAPI metadata for every response type.
- No persistence entities returned directly.

### 11.2 Candidate endpoint groups

- `/api/v1/auth`
- `/api/v1/businesses`
- `/api/v1/memberships`
- `/api/v1/customers`
- `/api/v1/invoices`
- `/api/v1/invoices/{id}/issue`
- `/api/v1/invoices/{id}/payments`
- `/api/v1/invoices/{id}/void`
- `/api/v1/invoices/{id}/history`
- `/api/v1/dashboard`
- `/health/live`
- `/health/ready`
- Internal automation endpoints not exposed by the public reverse proxy.

The exact endpoint design must be validated against use cases and HTTP semantics before implementation.

## 12. Frontend design

### 12.1 Feature structure

Candidate feature areas:

- Authentication.
- Application shell.
- Dashboard.
- Customers.
- Invoices.
- Invoice editor.
- Invoice detail and history.
- Payments.
- Administration/membership if included.

Use:

- Route-level lazy loading.
- Feature-owned components and composables.
- TanStack Vue Query for server state.
- Pinia only for genuine client/global state.
- Local form state.
- Generated API client and runtime schemas.

### 12.2 Required UI states

Every important feature must deliberately implement:

- Loading.
- Success.
- Empty.
- Validation error.
- Network/dependency error.
- Unauthorized/forbidden.
- Not found.
- Stale-edit conflict.
- Unexpected rendering failure.
- Retry/recovery action where safe.

### 12.3 Accessibility

Required:

- Semantic headings, landmarks, tables, forms, and buttons.
- Complete accessible names and descriptions.
- Keyboard-only operation.
- Visible focus.
- Focus movement/restoration for dialogs.
- Validation summary and field association.
- Live-region announcements for asynchronous results where appropriate.
- Colour contrast verification.
- Automated axe checks.
- Manual keyboard test script.

### 12.4 Performance

- Route lazy loading.
- Debounced and cancellable remote search.
- Server-side pagination.
- No unnecessary global reactivity.
- Bundle analysis and documented budget.
- Prevent duplicate requests.
- Avoid optimistic UI for operations whose rollback semantics are unclear.

## 13. Resilience policy

Create a resilience matrix with one row per dependency:

| Boundary | Failure types | Timeout | Retry owner | Max attempts | Circuit breaker/fallback | Terminal action |
|---|---|---:|---|---:|---|---|
| PostgreSQL | To be proven | TBD | EF/Npgsql strategy where safe | TBD | Readiness failure | Return safe error / outbox recovery |
| Dapr sidecar | To be proven | TBD | Dapr/operator policy | TBD | Readiness failure | Do not accept dependent work |
| Pub/Sub | At-least-once delivery | Component-specific | Dapr | Pinned policy | Dead-letter topic | Operator-visible failure |
| Workflow activity | Transient activity failure | Activity-specific | Dapr Workflow | Bounded | Durable retry | Failed workflow/delivery |
| Mailpit/SMTP | Timeout, 4xx/5xx, network | TBD | Workflow activity | Bounded | No fake success | Delivery failed |
| Browser GET | Network, selected 5xx | Client-specific | Vue Query | Small bounded count | User retry | Error state |
| Browser mutation | Timeout/unknown outcome | Client-specific | None unless idempotent | Usually 0 automatic | Reconcile by idempotency key | Honest unknown/error state |

No value marked TBD may reach production configuration without evidence and an ADR or configuration note.

## 14. Observability plan

### 14.1 Correlation

Propagate:

- W3C trace context.
- Request/trace ID.
- Event ID.
- Workflow instance ID.
- Causation ID.

Include safe business identifiers:

- Business ID.
- Invoice ID.
- Notification delivery ID.

Do not include:

- Passwords.
- Authentication cookies or tokens.
- Full customer address.
- Unnecessary customer name or email.
- Invoice line descriptions unless explicitly required and reviewed.

### 14.2 Required telemetry

Technical:

- HTTP request count, duration, status, and active requests.
- Database dependency duration/failure.
- Dapr publish and workflow operation duration/failure.
- Process/runtime metrics.

Domain:

- Invoices created and issued.
- Payment operations.
- Overdue transitions.
- Notification attempts, successes, terminal failures, and retry counts.
- Outbox backlog age and count.
- Failed workflows.

### 14.3 Health

- `/health/live`: process-only liveness.
- `/health/ready`: PostgreSQL, required Dapr readiness, and required startup state.
- Optional SMTP/Mailpit failure must be represented according to the documented degradation policy.

Required test:

- Stop each dependency individually and verify the intended health and user-visible behaviour.

## 15. Test strategy

### 15.1 Domain tests

- Money arithmetic and rounding.
- Tax calculation.
- Invoice totals.
- Every valid and invalid lifecycle transition.
- Payment edge cases.
- Due-date eligibility.
- Invoice-number rules.

### 15.2 Application tests

- Command/query handlers.
- Authorization decisions.
- Validation.
- Idempotency.
- Audit/outbox creation.
- Time-provider-driven behaviour.

### 15.3 PostgreSQL integration tests

- EF mappings and migrations.
- Unique constraints.
- Atomic invoice-number allocation under concurrency.
- Optimistic-concurrency conflicts.
- Payment transaction correctness.
- Outbox claiming with multiple publishers.
- Tenant query isolation.
- Pagination, filtering, and indexes.

Do not use EF InMemory for these behaviours.

### 15.4 API integration tests

- Complete response/status matrix.
- Problem Details schema.
- Authentication and antiforgery.
- Every authorization-matrix row.
- Cross-tenant IDs.
- Idempotency keys.
- ETag/version conflicts.
- Rate limits.
- Real readiness behaviour.

### 15.5 Dapr integration tests

- Publish/subscribe.
- Duplicate event.
- Inbox deduplication.
- Workflow timer.
- External events.
- Restart recovery.
- Retry and terminal failure.
- Trace propagation.

Use Dapr.Testcontainers only after its current API and limitations are verified. Pin the version. Maintain Compose-level tests as the final proof.

### 15.6 Frontend tests

- Components and composables.
- Generated-client wrapper and runtime validation.
- Async state presentation.
- Mutation and cache invalidation.
- Error containment.
- Pagination and filters.
- Accessibility.
- Keyboard behaviour.
- Malformed API response.

### 15.7 End-to-end tests

Run Playwright against the real Docker stack:

1. Owner signs in.
2. Owner creates customer.
3. Accountant creates draft invoice.
4. Accountant adds line items and sees server-confirmed totals.
5. Accountant issues invoice.
6. Workflow is visible/traced.
7. Controlled time or a short test due date triggers overdue evaluation.
8. Mailpit receives notification.
9. Payment is recorded.
10. Invoice becomes paid.
11. History shows the correct sequence.
12. Viewer cannot mutate.
13. User in another tenant cannot access the invoice.

### 15.8 Failure and chaos-style tests

- PostgreSQL unavailable.
- Redis unavailable.
- Dapr sidecar unavailable.
- Mailpit unavailable.
- Worker killed during workflow.
- Duplicate Pub/Sub delivery.
- Two users edit the same invoice.
- Two payments race against remaining balance.
- Two invoice allocations occur concurrently.
- API response deliberately malformed.

## 16. CI/CD plan

### 16.1 Pull-request pipeline

Parallel jobs where practical:

1. Repository policy
   - Secret scan.
   - License/dependency policy.
   - Generated-file policy.

2. Backend quality
   - Restore with locked dependencies.
   - Format verification.
   - Build with warnings as errors.
   - Architecture tests.
   - Unit tests.
   - Coverage report.

3. Frontend quality
   - Frozen lockfile install.
   - Lint.
   - Type check.
   - Unit/component tests.
   - Accessibility tests.
   - Coverage report.
   - Production build and bundle analysis.

4. Contract quality
   - Generate OpenAPI.
   - Generate frontend client/schemas.
   - Fail on source drift.
   - Run malformed-contract tests.

5. Integration
   - PostgreSQL tests.
   - API tests.
   - Dapr workflow/Pub/Sub tests.

6. Container
   - Build every final target.
   - Scan images.
   - Verify non-root runtime.
   - Start Compose stack.
   - Run health and smoke tests.

7. End-to-end
   - Run Playwright against Compose.
   - Preserve diagnostics on failure.

### 16.2 Main-branch/release evidence

- Immutable image tags tied to commit SHA.
- Software bill of materials where practical.
- Published test and coverage results.
- Migration verification.
- Release notes generated from intentional changes.

No real external deployment is required for the assessment unless explicitly chosen later. Public GitHub and clean Docker execution remain mandatory.

## 17. Documentation plan

### 17.1 README

Must contain:

- Purpose and feature summary.
- Architecture overview.
- Prerequisites.
- Exact clean-clone build/run/test/cleanup commands.
- Demo accounts and safe local credentials.
- Key user journeys.
- Service and port table.
- Health and observability instructions.
- Security model.
- Dapr purpose.
- Known limitations.
- Troubleshooting.

### 17.2 Architecture documentation

- Context and container diagrams.
- Domain model.
- Invoice lifecycle.
- Issue/payment transaction sequence.
- Outbox/Pub/Sub flow.
- Overdue workflow.
- Authentication and authorization.
- Failure and retry ownership.
- Observability flow.
- Deployment topology.

Diagrams must match the actual implementation.

### 17.3 ADR minimum set

- ADR-001: Modular monolith plus automation worker.
- ADR-002: PostgreSQL as business source of truth.
- ADR-003: Dapr Workflow/Pub/Sub adoption or fallback.
- ADR-004: EF Core transactional outbox and consumer inbox.
- ADR-005: Cookie authentication and antiforgery.
- ADR-006: Tenant authorization model.
- ADR-007: Invoice-number allocation.
- ADR-008: Money, currency, tax, and rounding.
- ADR-009: Invoice lifecycle and edit policy.
- ADR-010: OpenAPI client and runtime validation generation.
- ADR-011: Retry ownership and resilience policies.
- ADR-012: Observability backend and Compose profile.
- ADR-013: Migration deployment strategy.
- ADR-014: Time-zone and due-instant semantics.

Each ADR must include:

- Context.
- Decision.
- Alternatives considered.
- Evidence.
- Consequences.
- Failure modes.
- Reversal/fallback path.

## 18. Risk and uncertainty register

Maintain a live register with:

- Risk/doubt.
- Impact and likelihood.
- Evidence needed.
- Timebox.
- Decision.
- Fallback.
- Test proving the resolution.

Initial high-priority entries:

| Risk or doubt | Resolution evidence | Fallback |
|---|---|---|
| Dapr Workflow/SDK/runtime compatibility | Official compatibility plus executable Compose spike | Hangfire |
| Workflow survives restart and external-event timing | Kill/restart integration tests | Do not adopt Dapr workflow |
| Dapr Testcontainers API is sufficiently stable | Pinned proof-of-concept | Compose-based integration harness |
| Cookie auth and CSRF work cleanly with Vue/nginx | Playwright and API security tests | Standards-based OIDC |
| Data-protection keys support multiple API replicas | Two-replica session test | Single replica with limitation documented only temporarily; must resolve before submission |
| OpenAPI tool provides generated runtime validation | Malformed payload test and CI drift check | Kiota plus explicit Zod boundary |
| Invoice-number strategy meets stated semantics | Concurrent PostgreSQL test and ADR | Simpler unique non-gapless numbering with honest semantics |
| Due date/time-zone behaviour is unambiguous | Domain ADR and clock-controlled tests | Single documented business time zone |
| Tax calculation scope could imply legal correctness | Explicit simplified domain rules and disclaimer | Fixed demo tax model |
| Observability stack may overcomplicate default Compose | Clean-clone timing/reliability test | Optional Compose profile |
| Retry policies may amplify one another | Attempt-count analysis and failure test | Remove overlapping retry layer |
| Outbox publisher may duplicate messages | Crash-window integration tests | Must retain inbox idempotency; no weaker fallback |

Any new doubt with material impact must be added before implementation continues.

## 19. Implementation phases and gates

## Phase 0 — Evidence, decisions, and spikes

### Objective

Resolve architecture-affecting uncertainty before building product breadth.

### Work

- Establish repository.
- Copy the constitution and plan into the repository.
- Create evidence, traceability, and risk-register templates.
- Verify exact supported versions from primary sources.
- Spike Dapr Workflow restart/external-event behaviour.
- Spike Compose topology.
- Spike cookie auth/CSRF through nginx.
- Spike OpenAPI client and runtime schema generation.
- Decide observability backend.
- Write ADR-001 through ADR-005, ADR-010, ADR-012, and ADR-014.

### Gate

- Every baseline uncertainty has evidence or a chosen fallback.
- `docker compose up --build` works for the spike.
- No unpinned critical dependency.
- The final architecture is simpler than or equal to the proposed baseline.

## Phase 1 — Walking skeleton

### Objective

Prove the complete delivery path before adding domain complexity.

### Work

- Create solution/projects and Vue app.
- Add PostgreSQL migration pipeline.
- Add API and automation worker.
- Add Dapr sidecars.
- Add nginx same-origin proxy.
- Add live/ready endpoints.
- Add structured logging and trace propagation.
- Add root Dockerfile, Compose, `.env.example`, and initial CI.
- Create one trivial authenticated vertical request through the stack.

### Gate

- Clean clone builds and starts.
- API, web, database, Dapr, and worker become healthy.
- One request is traceable across the relevant components.
- Containers run non-root where applicable.
- CI builds backend/frontend and container images.

## Phase 2 — Identity, tenancy, and authorization

### Objective

Establish the security boundary before business features.

### Work

- ASP.NET Core Identity.
- Cookie and antiforgery configuration.
- Business and membership model.
- Seed safe demonstration users/roles.
- Policy and resource authorization.
- Authorization matrix tests.
- Data-protection key persistence.
- Login/logout Vue flow.

### Gate

- Anonymous, wrong-role, and cross-tenant tests pass.
- CSRF rejection is demonstrated.
- Two API replicas share valid authentication state.
- No production secret fallback exists.

## Phase 3 — Domain and persistence foundation

### Objective

Implement the financial model and prove its invariants.

### Work

- Resolve money/tax/rounding ADR.
- Implement value objects and invoice state model.
- EF Core mappings, constraints, and migrations.
- Atomic invoice-number allocator.
- Audit, outbox, and inbox schemas.
- Domain and PostgreSQL integration tests.

### Gate

- Concurrent allocation test proves uniqueness/semantics.
- Financial calculations are deterministic and thoroughly tested.
- Invalid lifecycle states are rejected.
- Migration applies from an empty database and from the previous schema snapshot.

## Phase 4 — Contract and frontend foundation

### Objective

Create a trustworthy full-stack boundary and production-ready Vue shell.

### Work

- Versioned API and Problem Details.
- Generate OpenAPI.
- Generate TypeScript client/runtime schemas.
- Configure TanStack Vue Query.
- Configure minimal Pinia usage.
- Route lazy loading.
- Root and feature-level error containment.
- Application shell, navigation, accessibility baseline.

### Gate

- Generated artifacts have zero drift.
- Malformed API payload is rejected visibly and safely.
- 401, 403, 404, 409, validation, and unexpected error states render correctly.
- Keyboard navigation works through shell and authentication.

## Phase 5 — Customer vertical slice

### Objective

Deliver the first complete production-quality business feature.

### Work

- Customer create/list/detail/edit.
- Server pagination/filter/search.
- Tenant authorization.
- Validation.
- Complete UI states.
- Unit, integration, frontend, accessibility, and E2E tests.

### Gate

- Feature satisfies every implementation-gate item.
- Cross-tenant customer access is blocked.
- Search is debounced/cancelled.
- Pagination is real and server-connected.

## Phase 6 — Draft invoice vertical slice

### Objective

Deliver invoice creation and editing with trustworthy financial calculations.

### Work

- Draft creation.
- Line-item editing.
- Server-side totals.
- Invoice preview.
- Optimistic concurrency.
- Audit history.
- Complete frontend state and accessibility.

### Gate

- Server is authoritative for totals.
- Stale concurrent edit returns and displays a conflict.
- Line and total edge cases pass.
- Draft workflow is proven end-to-end.

## Phase 7 — Issue and payment lifecycle

### Objective

Complete synchronous invoice lifecycle correctness.

### Work

- Issue command.
- Atomic number allocation.
- Payment recording.
- Partial/full payment transitions.
- Void policy.
- Outbox event creation.
- Idempotency keys.
- Audit history.

### Gate

- Concurrent issue/allocation passes.
- Duplicate command tests pass.
- Concurrent payments cannot overpay.
- Every status transition and authorization path is proven.

## Phase 8 — Dapr overdue automation

### Objective

Implement the required simulated overdue notification using proven durable processing.

### Work

- Outbox publisher.
- Dapr Pub/Sub subscriptions.
- Inbox idempotency.
- Durable workflow.
- External paid/void/due-date events.
- Mailpit delivery.
- Attempt/failure records.
- Reconciliation.
- Failure and restart tests.

### Gate

- Every workflow proof listed in Section 9.3 passes.
- Duplicate delivery is safe.
- Workflow and business state can be reconciled.
- No user-visible success is reported before the relevant durable guarantee exists.

## Phase 9 — Dashboard, history, and UX completion

### Objective

Complete the reviewer-visible product experience without compromising server-side scale.

### Work

- Tenant-scoped summary queries.
- Invoice list filtering/sorting/pagination.
- Dashboard metrics.
- Activity/history presentation.
- Responsive design.
- Empty/loading/error/conflict states.
- Accessibility polish.
- Bundle analysis.

### Gate

- No unbounded dashboard query.
- No non-functional controls.
- Accessibility automated checks pass.
- Manual keyboard script passes.
- Bundle is within the documented budget or justified.

## Phase 10 — Resilience and observability hardening

### Objective

Prove behaviour when dependencies and processes fail.

### Work

- Final resilience matrix.
- Timeouts and bounded retries.
- Dead-letter/terminal failure handling.
- Readiness/liveness dependency tests.
- OTel traces and metrics.
- Domain dashboards or documented queries.
- Operational runbooks.

### Gate

- Every dependency failure has tested expected behaviour.
- No retry amplification.
- A notification failure can be diagnosed from one trace/workflow ID.
- Outbox backlog and failed workflows are operator-visible.

## Phase 11 — Security, performance, and delivery hardening

### Objective

Remove production-trust gaps.

### Work

- Threat-model review.
- OWASP-oriented authorization and abuse tests.
- Secret and dependency scans.
- Container scan and non-root verification.
- Representative load/concurrency profile.
- Migration deployment test.
- Graceful shutdown test.
- Clean-clone rehearsal.

### Gate

- No critical/high untriaged security findings.
- No hardcoded secrets.
- No known broken build/run/test instructions.
- Performance findings are resolved or honestly documented.
- Graceful shutdown preserves/retries asynchronous work correctly.

## Phase 12 — Submission proof and interview preparation

### Objective

Make every claim inspectable and prepare to defend every decision.

### Work

- Complete requirements traceability.
- Execute clean-clone build/run/test.
- Execute final E2E and failure suite.
- Verify all docs against code.
- Record known limitations.
- Produce architecture and trade-off summary.
- Prepare demonstration script.
- Prepare interview question bank based on ADRs, failures, and alternatives.
- Verify repository is public-ready with no sensitive history.

### Gate

- Every constitution submission-gate item passes.
- Every README command is re-run successfully.
- Every production-grade claim links to evidence.
- Codex performs a hostile review looking for contradictions, races, auth bypasses, fake health, dead configuration, unbounded queries, and false test claims.
- No material concern remains without an explicit, defensible limitation.

## 20. Four-week execution schedule

### Week 1 — Prove foundations

- Phase 0.
- Phase 1.
- Phase 2.
- Begin Phase 3.

Exit condition:

- Risky technology choices proven.
- Clean-clone skeleton works.
- Identity and tenant boundary are established.

### Week 2 — Complete synchronous product

- Finish Phase 3.
- Phase 4.
- Phase 5.
- Phase 6.
- Begin Phase 7.

Exit condition:

- Secure full-stack customer and draft-invoice journeys work.
- Contract generation and frontend foundations are proven.

### Week 3 — Demonstrate SE3 depth

- Finish Phase 7.
- Phase 8.
- Phase 9.

Exit condition:

- Issue/payment concurrency and idempotency are proven.
- Durable overdue notification works through Dapr.
- Product experience is complete.

### Week 4 — Break, prove, and communicate

- Phase 10.
- Phase 11.
- Phase 12.

Exit condition:

- The system has survived deliberate failure testing.
- CI and clean-clone paths pass.
- Documentation and interview preparation are complete.

### Schedule protection rule

If a phase overruns:

1. Stop optional scope.
2. Use the pre-agreed fallback for an unresolved tool.
3. Preserve required journeys, security, correctness, tests, and clean delivery.
4. Never respond by deleting verification or hiding limitations.

## 21. Feature definition of done

A feature is done only when:

- It maps to a requirement.
- Its domain rules are explicit.
- Authentication and resource authorization are implemented.
- Validation exists at appropriate boundaries.
- Concurrency and duplicate behaviour are defined.
- Database constraints support important invariants.
- API contract and generated frontend code agree.
- Complete frontend states exist.
- Accessibility is checked.
- Structured logs/traces/metrics exist where relevant.
- Risk-based automated tests pass.
- Documentation matches implementation.
- Clean Docker execution remains intact.

## 22. Final submission definition of done

The submission is ready only when:

- Public GitHub repository contains no secrets or sensitive history.
- Root README provides exact working build, run, test, and cleanup instructions.
- Root Dockerfile is runnable.
- Compose starts the complete application.
- Database migrations are safe and tested.
- All required full-stack journeys work.
- Cross-tenant and wrong-role access tests pass.
- Financial calculations and lifecycle invariants pass.
- Concurrency and idempotency tests pass.
- Dapr workflow restart, duplicate, external-event, and retry tests pass.
- Liveness/readiness behaviour is truthful.
- Logs, metrics, and traces provide diagnostic continuity.
- Frontend runtime validation, error containment, accessibility, and real pagination are verified.
- CI passes from a clean clone.
- Container and dependency scans have no untriaged critical/high findings.
- Requirements traceability is complete.
- Known limitations are honest and bounded.
- Every major decision has an ADR and a defensible alternative analysis.
- The demo and interview answers are based on implemented evidence.

## 23. Mandatory review questions

Before Codex marks any meaningful change complete, it must answer:

1. What requirement does this satisfy?
2. What can this break?
3. What happens if it executes twice?
4. What happens under real concurrency?
5. What happens if the process restarts mid-operation?
6. What happens with multiple replicas?
7. Can another user or tenant reach this resource?
8. Are failure, timeout, and retry semantics explicit?
9. Can an operator diagnose failure without reproducing it locally?
10. Does the frontend present every meaningful state?
11. Is the behaviour accessible by keyboard and assistive technology?
12. Does the API/runtime contract prove what TypeScript assumes?
13. Does Docker still work from a clean clone?
14. Do documentation and code make the same claim?
15. Is this complexity earning its operational cost?

If any answer is uncertain, the change is not complete.
