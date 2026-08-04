# SE3 Engineering Constitution

This is the standing rule set for designing, implementing, reviewing, and submitting the SE3 Full-Stack project. When a decision conflicts with this constitution, either change the decision or record an explicit Architecture Decision Record (ADR) explaining why the exception is safer.

## 1. Governing principles

### SE3-01 — Evidence over claims

Never describe something as production-grade, secure, scalable, tested, observable, or resilient unless the repository contains evidence that a reviewer can run or inspect.

- Documentation must match the current code.
- A test name or comment is not proof unless the test exercises the claimed behaviour.
- A configuration file is not proof unless the running application uses it.
- A health endpoint is not proof unless it checks the dependency it claims to represent.

### SE3-02 — Deliver the entire brief

Complete the required full-stack user journeys before adding optional sophistication.

- Backend, frontend, integration, Docker, tests, and documentation are all mandatory deliverables.
- No surface may remain boilerplate.
- Optional architecture must never put a required journey at risk.
- Maintain a traceability table from every brief requirement to implementation and tests.

### SE3-03 — Depth must solve a real risk

Demonstrate senior depth through hard, domain-specific correctness—not through the number of technologies used.

For the Invoice Tracker, the primary depth areas are:

- Atomic invoice-number allocation.
- Money and tax calculations.
- Valid invoice lifecycle transitions.
- Tenant and resource isolation.
- Durable overdue processing.
- Idempotent event and notification handling.
- Correctness under concurrent requests and retries.

### SE3-04 — Ask “How can this break?”

For every feature, examine:

- Concurrent use.
- Duplicate delivery or submission.
- Partial failure.
- Timeout or unavailable dependency.
- Process restart.
- Multiple application replicas.
- Stale data.
- Malformed or unauthorized input.
- Large data volume.
- Deployment during in-flight work.

The answer must result in implementation, a test, monitoring, or an explicitly accepted limitation.

### SE3-05 — Prefer understandable restraint

Use the simplest architecture that safely demonstrates the required SE3 qualities.

- Do not create a service merely to claim microservices.
- Do not add patterns with no identified failure mode.
- Do not add abstractions without a second concrete use or a clear boundary benefit.
- Every dependency must have a documented purpose, operational cost, and failure behaviour.

## 2. Domain, data, and consistency

### SE3-06 — PostgreSQL is the business source of truth

Invoices, customers, payments, memberships, audit records, delivery records, and outbox/inbox messages belong in the relational domain model.

- Dapr workflow state coordinates automation; it does not replace business records.
- Cache and workflow data must be rebuildable or reconcilable from the business database.
- Database constraints are the final defence for invariants that can be expressed relationally.

### SE3-07 — Make invalid domain states difficult to represent

- Use value objects for Money, Currency, InvoiceNumber, EmailAddress, and similar concepts.
- Calculate authoritative totals server-side.
- Define an explicit invoice state machine.
- Reject invalid transitions centrally in the domain layer.
- Store timestamps in UTC and define display-zone behaviour.
- Use decimal arithmetic and explicit rounding rules for financial values.

### SE3-08 — Make concurrency explicit

Every read-then-write operation must be reviewed for a race window.

- Use atomic SQL, constraints, optimistic concurrency, or an appropriate transaction isolation level.
- Never rely only on “check whether it exists, then insert/update.”
- Return a meaningful conflict response when concurrent work loses a race.
- Test the race with genuinely concurrent requests against PostgreSQL.

### SE3-09 — Design every asynchronous operation for at-least-once execution

Assume that messages, jobs, workflow activities, and user submissions can happen more than once.

- Give events stable IDs.
- Use inbox records or equivalent deduplication for consumers.
- Use idempotency keys for important commands.
- Make notification delivery re-entrant.
- Record attempts and terminal failures.
- Never assume a timeout means an operation did not complete.

### SE3-10 — State consistency boundaries must be visible

- Write the invoice change, audit record, and outbox event in one EF Core/PostgreSQL transaction.
- Publish outbox records asynchronously.
- Do not claim Dapr’s state-store outbox covers an EF Core transaction.
- Document where consistency is strong and where it is eventual.
- Provide reconciliation for business state and workflow state that may drift.

### SE3-11 — Bound every collection and expensive operation

- All collection endpoints are paginated.
- Filtering and sorting execute server-side.
- Apply maximum page sizes and input limits.
- Add indexes based on demonstrated access paths.
- Avoid loading an entire table to calculate a dashboard.
- Rate-limit abuse-prone or externally costly operations.

## 3. Security and privacy

### SE3-12 — Authentication is not authorization

- Authenticate users through a standard framework.
- Authorize every protected function.
- Authorize every tenant-owned resource, even when the caller supplies a valid ID.
- Use policy/resource-based authorization rather than scattered role checks.
- Test anonymous, wrong-role, cross-user, and cross-tenant access.
- Never trust tenant IDs, user IDs, totals, roles, or status fields supplied by the browser.

### SE3-13 — No secrets in source, images, logs, or defaults

- The application must fail fast when required production secrets are absent.
- Commit only safe templates such as `.env.example`.
- Local demo credentials must be clearly scoped as non-production data.
- Never silently fall back to a known signing key or password.
- Scan the repository and container image for secrets.
- If a secret is exposed, rotate it; deleting the latest copy is not sufficient.

### SE3-14 — Treat cookie and token choices as threat-model decisions

For the browser application:

- Prefer secure, HTTP-only, same-site authentication cookies behind a same-origin proxy.
- Protect state-changing cookie-authenticated requests against CSRF.
- Configure secure cookie behaviour for non-development environments.
- Do not store long-lived bearer tokens in local storage.
- Persist data-protection keys appropriately if multiple replicas share cookie authentication.

### SE3-15 — Minimise exposure

- Return consumer-specific DTOs, never persistence entities.
- Prevent mass assignment by defining command/request models explicitly.
- Do not log invoice content, credentials, tokens, or unnecessary personal data.
- Add safe security headers and a Content Security Policy.
- Validate file type, size, name, and content if file uploads are ever introduced.

## 4. API and full-stack contracts

### SE3-16 — One contract, enforced at multiple layers

- ASP.NET OpenAPI is the contract source.
- Generate the TypeScript API client and runtime validation schemas from it.
- Fail CI when generated artifacts drift.
- Validate important responses at runtime in the frontend.
- Use one consistent RFC Problem Details error format.
- Document every meaningful status code.
- Version the public API deliberately.

### SE3-17 — HTTP semantics and retry semantics must agree

- GET and other safe reads may be retried conservatively.
- Do not automatically retry non-idempotent commands without an idempotency key.
- Use 409 for state/concurrency conflicts, 401 for unauthenticated, 403 for unauthorized, 404 without leaking inaccessible resource existence, and appropriate validation responses.
- Timeouts, cancellations, and correlation/trace context must propagate across boundaries.

## 5. Frontend engineering

### SE3-18 — Separate server state, client state, and form state

- Use TanStack Vue Query for remote/server state.
- Use Pinia only for genuine cross-feature client state.
- Keep short-lived presentation and form state local unless sharing is required.
- Avoid parallel booleans that allow impossible async states.
- Invalidate or update query caches deliberately after mutations.

### SE3-19 — Every screen must handle the complete state model

Each important screen or feature must define and test:

- Initial/idle.
- Loading.
- Success.
- Empty.
- Validation failure.
- Authorization failure.
- Conflict/stale update.
- Recoverable dependency/network failure.
- Unexpected rendering/runtime failure.

Use root-level error reporting plus feature-level `onErrorCaptured` containment and useful recovery actions.

### SE3-20 — Accessibility and performance are correctness concerns

- Use semantic HTML before ARIA.
- Ensure keyboard navigation, visible focus, correct labels, and focus restoration.
- Announce validation and asynchronous results appropriately.
- Test automated accessibility and critical keyboard journeys.
- Lazy-load routes.
- Set and check a bundle-size budget.
- Debounce remote search and cancel obsolete requests.
- Never implement pagination controls that do not control server pagination.

## 6. Dapr and distributed processing

### SE3-21 — Dapr must own a real distributed-systems concern

Approved Dapr responsibilities:

- Durable invoice lifecycle workflow.
- Durable timer until an invoice due date.
- External workflow events for paid, voided, or due-date-changed invoices.
- Pub/Sub transport for lifecycle events.
- Trace propagation and defined resiliency policies.

Dapr must not:

- Replace the relational invoice model.
- Hide unclear service boundaries.
- Be used only as résumé decoration.
- Make the clean-clone experience unreliable.

### SE3-22 — One retry owner per failure boundary

Avoid retry amplification.

- Workflow activity retry handles durable business-process retries.
- Dapr Pub/Sub handles message redelivery.
- `Microsoft.Extensions.Http.Resilience` handles selected external HTTP calls.
- EF/Npgsql execution strategy handles known transient database failures only around safe transaction delegates.
- Frontend retry is limited to safe or explicitly idempotent operations.

Record the maximum attempts, backoff, timeout, and terminal action for each boundary.

### SE3-23 — Horizontal scaling must not change correctness

- No critical session, lock, deduplication, queue, workflow, or cache state may live only in one process.
- Multiple replicas must produce the same authorization and business outcomes.
- Cache failure must not corrupt business state.
- If a local-development component differs from the production choice, document the mapping and the behavioural assumptions.

## 7. Reliability and operations

### SE3-24 — Health checks must be truthful

- Liveness answers whether the process should be restarted.
- Readiness answers whether the instance can serve traffic.
- Readiness checks real required dependencies such as PostgreSQL and Dapr.
- Optional dependency failure should report degraded behaviour where appropriate, not necessarily kill the application.
- Test health behaviour with a dependency deliberately unavailable.

### SE3-25 — Design observability with the feature

Every important request and asynchronous process must emit enough telemetry to answer:

- What happened?
- To which tenant and business entity?
- Which request, trace, workflow, event, and delivery attempt were involved?
- How long did it take?
- Was it retried?
- What failed and is operator action required?

Use:

- Structured logs to stdout.
- OpenTelemetry traces and metrics.
- Correlation and trace IDs propagated end-to-end.
- Domain metrics with bounded-cardinality labels.
- No sensitive information in telemetry.

### SE3-26 — Degrade deliberately

- Define whether each dependency is required, optional, or asynchronously recoverable.
- Apply circuit breakers only where failing fast or falling back is meaningful.
- Do not retry permanent validation, authorization, or constraint failures.
- Use dead-letter handling or a terminal failed state for poison messages.
- Surface user-facing status honestly; never report “sent” before delivery semantics justify it.

## 8. Testing and verification

### SE3-27 — Tests follow risk, not just code structure

Required layers:

- Domain unit tests for calculations and transitions.
- Authorization policy and resource-isolation tests.
- API integration tests against real PostgreSQL.
- Concurrent race tests.
- Dapr workflow and Pub/Sub integration tests.
- Frontend component/composable tests.
- Contract/runtime-validation tests.
- Accessibility tests.
- Playwright end-to-end tests against the Docker stack.

The most important unhappy paths must receive more attention than simple CRUD success cases.

### SE3-28 — Test the actual claim

- A concurrency claim needs concurrent execution.
- A retry claim needs a dependency that fails and recovers.
- A readiness claim needs a real dependency failure.
- An idempotency claim needs duplicate delivery.
- A horizontal-scaling claim needs requests handled by distinct instances or an equivalent integration proof.
- A Docker claim needs a clean-clone build.

### SE3-29 — Test infrastructure must resemble production where behaviour matters

- Do not use EF InMemory for relational constraints, transactions, SQL, or concurrency.
- Use PostgreSQL containers for database integration.
- Pin Dapr and dependency image versions.
- Keep tests deterministic; control time through an injected clock.
- Never rely on arbitrary sleeps when a deterministic signal can be awaited.

## 9. Delivery and documentation

### SE3-30 — A clean clone is the primary reviewer journey

From a fresh clone, the documented commands must:

1. Build successfully.
2. Start the complete system.
3. Apply or execute database migrations safely.
4. Provide demo data or a documented onboarding flow.
5. Run tests.
6. Stop and clean up without manual repair.

Run this exact journey in CI.

### SE3-31 — Containers must reflect production intent

- Use multi-stage builds.
- Run application containers as non-root.
- Include `.dockerignore`.
- Pin base-image versions and scan final images.
- Add graceful shutdown.
- Do not combine unrelated production processes in one container.
- Do not bake secrets into an image.
- Keep the mandatory Dockerfile runnable independently of an IDE.

### SE3-32 — Database evolution is a deployment concern

- Use reviewed EF Core migrations; never `EnsureCreated` for the deliverable.
- Test migrations from an earlier schema.
- Prefer forward-compatible expand/migrate/contract changes.
- Do not allow every application replica to race to migrate on startup.
- Document backup, rollback, and data-compatibility assumptions.

### SE3-33 — CI is part of the product

CI must enforce:

- Formatting and analyzers.
- Warnings-as-errors build.
- Type checking and linting.
- Unit and integration tests.
- Coverage reporting with sensible, risk-aware gates.
- OpenAPI/generated-client drift checks.
- Dependency and secret scanning.
- Container build and scanning.
- Docker-stack smoke/end-to-end tests.

### SE3-34 — Documentation is an executable contract

The README and architecture documentation must explain:

- What the system does.
- Exact build, run, test, and cleanup commands.
- Architecture and component responsibilities.
- Data and workflow state transitions.
- Security model.
- Consistency, retry, and failure semantics.
- Observability and health endpoints.
- Important trade-offs and rejected alternatives.
- Known limitations stated honestly.

Validate commands and examples in CI wherever possible.

## 10. Working gates

### Design gate — before implementing a feature

Do not begin implementation until we can answer:

- Which brief requirement does this satisfy?
- What is the user journey?
- What data and invariant are involved?
- Who may perform it, and on which resources?
- What is the transaction boundary?
- What happens concurrently?
- What happens on duplicate submission or delivery?
- What can time out or become unavailable?
- Which telemetry proves it worked?
- Which tests prove the important failure modes?
- Is an ADR required?

### Implementation gate — before considering a feature complete

- Backend validation and authorization exist.
- Database constraints and concurrency behaviour are correct.
- API contract and generated frontend client agree.
- Frontend handles loading, empty, error, conflict, and success states.
- Accessibility and keyboard behaviour are checked.
- Logs, traces, and metrics are present.
- Unit/integration/frontend tests cover the risk.
- Documentation and diagrams still match reality.

### Pull-request gate

Every review asks:

1. What can this break?
2. What happens twice?
3. What happens concurrently?
4. What happens after restart?
5. What happens with multiple replicas?
6. Can one user access another tenant’s data?
7. Are retries safe and bounded?
8. Can an operator diagnose failure?
9. Does the clean-clone build still work?
10. Is every claim in the change demonstrably true?

### Submission gate

The project is not ready to submit until:

- Every brief requirement maps to passing evidence.
- The full Docker stack builds from a clean clone.
- All test suites and quality gates pass.
- No committed secrets or known critical vulnerabilities remain.
- Cross-tenant and wrong-role authorization tests pass.
- Concurrency and idempotency tests pass.
- Readiness fails when required dependencies fail.
- End-to-end overdue notification behaviour is demonstrated.
- Generated contracts are current.
- Documentation matches the final repository.
- Known limitations and trade-offs are explicit.
- We can explain and defend every major architectural choice without hand-waving.

## 11. Decision priority

When time or trade-offs force a choice, use this order:

1. Required end-to-end behaviour.
2. Security and data correctness.
3. Hard domain invariants and concurrency.
4. Integration correctness.
5. Failure handling and operability.
6. Risk-based tests.
7. Accessibility and user experience.
8. Clean-clone delivery and documentation.
9. Optional infrastructure sophistication.

Never sacrifice items 1–8 merely to showcase item 9.
