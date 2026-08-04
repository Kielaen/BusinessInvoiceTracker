# Verification Ledger

This is an append-only-in-spirit record of material Phase 0 checks. Corrections add
context; they do not erase failed attempts.

## Status vocabulary

- `PASS`: the recorded command produced the stated evidence.
- `FAIL`: the command ran and disproved or failed to prove the claim.
- `BLOCKED`: the command could not complete because a prerequisite was absent.
- `PENDING`: not yet executed.

## Ledger

| ID | Date | Requirement or claim | Command/evidence | Result | Status |
|---|---|---|---|---|---|
| V-0001 | 2026-07-30 | Governing documents were read before implementation | Complete reads of both source Markdown files; the 1,398-line plan was paged after initial terminal truncation | Constitution and plan read completely | PASS |
| V-0002 | 2026-07-30 | Existing repository work must be preserved | Target path, ancestor instructions and Git inspection | Target did not exist; no prior work was overwritten | PASS |
| V-0003 | 2026-07-30 | Governing documents are preserved in the repository | SHA-256 source/copy comparison | Constitution `BF1D...176B`; plan `711C...9C3`; both equal | PASS |
| V-0004 | 2026-07-30 | Required local toolchain is available | Version commands for .NET, Node, npm, Docker, Compose, Dapr and Git | Global .NET 9, Node/npm/Git present; Docker/Compose/Dapr absent | FAIL |
| V-0005 | 2026-07-30 | A Git repository exists at the dedicated path | `git init`; `git status --short --branch` | Empty repository initialized; all new work remains visible/untracked | PASS |
| V-0100 | 2026-07-30 | Pinned .NET/Dapr SDK compatibility | Official support sources; SDK 10.0.302 locked restores; warning-as-error Release builds; vulnerability scans | Both net10 projects compile; no vulnerable NuGet packages; runtime 1.17.7 execution belongs to V-0101/2 | PASS |
| V-0101 | 2026-07-30 | Dapr durable timer survives worker and sidecar restart | `spikes/compose/verify-compose.ps1` | Script and YAML parse; execution cannot start because `docker` is absent | BLOCKED |
| V-0102 | 2026-07-30 | Dapr event raised before wait is buffered and wins timer race | `spikes/compose/verify-compose.ps1` | SDK 1.17.9 compile and primary docs pass; runtime execution blocked | BLOCKED |
| V-0103 | 2026-07-30 | Pinned topology starts from clean copy | `spikes/compose/verify-clean-copy.ps1` | Clean copy created and script invoked; stopped at `docker compose version` because Docker is absent | BLOCKED |
| V-0104A | 2026-07-30 | Cookie API semantics and CSRF work without nginx | `spikes/auth-contract/verify-local.ps1` | Anonymous 401/no redirect, missing CSRF 400, wrong role 403 | PASS |
| V-0104B | 2026-07-30 | nginx/Vue/API same-origin integration | Compose verification script | YAML and PowerShell syntax pass; runtime blocked by absent Docker | BLOCKED |
| V-0105 | 2026-07-30 | Cookie and antiforgery work across two API processes | `spikes/auth-contract/verify-local.ps1` | Replica 1 cookie accepted by replica 2; replica 2 token accepted by replica 1 | PASS |
| V-0106 | 2026-07-30 | Generated client/runtime schemas cover risky shapes | Release OpenAPI build; generator; Vue build; seven Vitest fixtures; npm/NuGet audits | All pass; one test file/seven tests; zero reported vulnerabilities | PASS |
| V-0107A | 2026-07-30 | Default telemetry has no viewer dependency | JSON stdout plus conditional OpenTelemetry configuration; warning-as-error builds | Both apps compile and local auth checks pass without OTLP endpoint | PASS |
| V-0107B | 2026-07-30 | Optional viewer starts and receives signals | Digest-pinned Aspire profile and override | Runtime visibility assertion blocked by absent Docker | BLOCKED |
| V-0199 | 2026-07-30 | Exact Phase 0 gate passes | Clean-copy gate invocation plus all V-01xx evidence | Required Compose, Dapr restart/event, nginx and optional-viewer checks remain blocked | BLOCKED |
| V-0101-R1 | 2026-07-31 | Dapr durable timer survives worker and sidecar restart | `spikes/compose/verify-compose.ps1 -Observability` and clean-copy rerun | Timer completed after deterministic worker/sidecar stop and restart against PostgreSQL v2 actor state | PASS |
| V-0102-R1 | 2026-07-31 | Dapr event raised before wait is durably buffered | Same two Compose runs | Event raised during the pre-registration timer completed the workflow as `external-event` | PASS |
| V-0103-R1 | 2026-07-31 | Pinned topology starts from a clean copy | `spikes/compose/verify-clean-copy.ps1` | Built and ran from guarded temp copy `business-invoice-phase0-clean-efff004baa7741dca6bd0060aa67e43b`; cleanup passed | PASS |
| V-0104B-R1 | 2026-07-31 | nginx/Vue/API same-origin integration | Both Compose runs | Anonymous API returned 401 without redirect; antiforgery fetch/login passed; unsafe request without token returned 400 | PASS |
| V-0107B-R1 | 2026-07-31 | Optional viewer receives useful signals | `spikes/compose/verify-compose.ps1 -Observability` | Dashboard telemetry API reported logs, traces, and metrics for both .NET services | PASS |
| V-0199-R1 | 2026-07-31 | Exact Phase 0 gate passes | All Phase 0 evidence plus final clean-copy and optional-profile runs | Every baseline uncertainty has evidence or bounded fallback; final topology is no broader than the baseline | PASS |
| V-0200 | 2026-07-31 | Locked product builds and dependency checks pass | `scripts/verify.ps1` | .NET 10.0.302 Release build: 0 warnings/errors; generated client current; 2 Vitest tests; Vue build; npm/NuGet scans report no vulnerabilities | PASS |
| V-0201 | 2026-07-31 | Product topology becomes healthy | `scripts/verify-compose.ps1 -Build` | PostgreSQL, API, and automation health checks passed; web and both Dapr sidecars ran | PASS |
| V-0202 | 2026-07-31 | Authenticated same-origin request traverses the stack | Same Compose run | Anonymous `/auth/me` returned 401; login plus refreshed CSRF token accepted mutation; final probe status was `processed` | PASS |
| V-0203 | 2026-07-31 | Duplicate delivery is harmless | Same Compose run; two repeat posts to worker | Both repeats returned 204 and structured logs recorded `Ignored duplicate`; unique inbox/receipt constraints retained one effect | PASS |
| V-0204 | 2026-07-31 | Async path is traceable | Structured logs for message `7db553dd-0931-49ec-9e0b-7d5962a899cf` | API publisher and Dapr-delivered worker request share W3C trace ID `18bead0b20603a26fd3fab6fbf77787b`; stable message ID spans persisted boundaries | PASS |
| V-0205 | 2026-07-31 | Application containers are non-root | Docker image config inspection in Compose verifier | API `1654`, automation `1654`, nginx `101` | PASS |
| V-0206 | 2026-07-31 | Optional viewer receives product signals | `scripts/verify-compose.ps1 -Observability` | Dashboard telemetry API reported logs, traces, and metrics for `invoice-api` and `invoice-automation` | PASS |
| V-0207 | 2026-07-31 | Product builds and starts from clean repository material | Guarded temp copy plus `scripts/verify-compose.ps1 -Build -Cleanup` | Fresh copy built all images, migrated an empty database, completed E2E and duplicate checks, and removed containers/volumes | PASS |
| V-0299 | 2026-07-31 | Phase 1 gate passes | V-0200 through V-0207; `.github/workflows/ci.yml` | All executable product gate conditions passed; CI jobs mirror locally passing commands, though no remote runner was available | PASS |
| V-0300 | 2026-07-31 | Pinned Phase 2 backend and frontend compile without suppressed warnings | `scripts/verify.ps1` | .NET SDK 10.0.302 locked restore and Release build passed with 0 warnings/errors; NuGet/npm scans found 0 vulnerabilities; three Vitest checks and Vue production build passed | PASS |
| V-0301 | 2026-07-31 | Identity and tenancy persist through the real database migration and explicit seed | Compose `migrate` service; `20260731083706_AddIdentityAndTenancy` | Identity tables, businesses, composite-key memberships and stable Development fixtures migrated/seeded; repeated migrate runs passed | PASS |
| V-0302 | 2026-07-31 | Required Phase 2 authorization matrix is enforced | `scripts/verify-phase2.ps1` | Anonymous 401; Viewer and Accountant reads 200; Viewer Owner-only request 403; Owner request 200; no-membership, inactive and other-tenant requests 404 | PASS |
| V-0303 | 2026-07-31 | Unsafe cookie-authenticated requests reject missing CSRF proof | `scripts/verify-phase2.ps1` | Authenticated logout without the request token returned RFC Problem status 400 and did not end the session | PASS |
| V-0304 | 2026-07-31 | Owner membership endpoint executes after authorization | First `scripts/verify-phase2.ps1` run | Authorization succeeded, but EF Core could not translate ordering after DTO construction; endpoint returned 500 | FAIL |
| V-0304-R1 | 2026-07-31 | Owner membership endpoint executes after authorization | Order database fields before DTO projection; rebuild; full matrix rerun | Owner membership list returned 200 and every earlier matrix row still passed | PASS |
| V-0305 | 2026-07-31 | Product authentication and antiforgery state work across API replicas | `scripts/verify-phase2.ps1`; `api-replica` Compose profile | Replica accepted the primary-issued Identity cookie (200), issued a valid antiforgery token, completed logout (204), and the primary then returned 401 | PASS |
| V-0306 | 2026-07-31 | Demonstration identity configuration cannot become a production fallback | `scripts/verify-phase2.ps1`; missing-secret fixture; Production migrate run | Compose rejected omitted `DEMO_USER_PASSWORD`; seeder rejected `ASPNETCORE_ENVIRONMENT=Production` even with a password | PASS |
| V-0307 | 2026-07-31 | Vue login/logout and generated runtime contracts match the versioned API | OpenAPI Release generation; Hey API generation; Vitest; `npm run build` | Current-user membership shape validated at runtime; Vue authenticates at `/api/v1/auth/login`, refreshes CSRF, and exposes logout/delivery actions | PASS |
| V-0308 | 2026-07-31 | Phase 1 delivery remains correct after Identity replacement | `scripts/verify-compose.ps1` | Identity login, refreshed CSRF, PostgreSQL outbox, Dapr delivery, worker receipt, duplicate handling and non-root assertions all passed | PASS |
| V-0309 | 2026-07-31 | Phase 2 builds and passes from clean repository material | `scripts/verify-clean-copy.ps1` | Guarded source copy excluded Git/tools/build output, built a distinct Compose project, migrated an empty database, passed V-0302/3/5/6 behaviours, removed isolated volumes, and restored the main stack with its volumes retained | PASS |
| V-0399 | 2026-07-31 | Exact Phase 2 gate passes | V-0300 through V-0309 and ADR-006 | Anonymous/wrong-role/cross-tenant, CSRF, two-replica auth state and no-production-fallback conditions all have executable passing evidence | PASS |
| V-0400 | 2026-07-31 | Material Phase 3 choices precede implementation | ADR-007, ADR-008, ADR-009, ADR-014 and current primary documentation | Numbering, rounding/tax, lifecycle/edit, due-instant and concurrency semantics are explicit with alternatives and fallbacks | PASS |
| V-0401 | 2026-07-31 | Pinned solution builds and dependency checks pass with the domain test project | `scripts/verify.ps1` | Locked .NET 10.0.302 restore; Release build 0 warnings/errors; 14 .NET tests and 3 Vitest tests; Vue build; NuGet/npm scans report no vulnerabilities | PASS |
| V-0402 | 2026-07-31 | Financial calculations and due instant are deterministic | `FinancialModelTests` | Per-line net/tax aggregation, fractional quantity, midpoint AwayFromZero, zero tax, invalid scale/range/currency, and Johannesburg next-midnight UTC instant passed | PASS |
| V-0403 | 2026-07-31 | Invalid invoice lifecycle states are rejected centrally | `InvoiceLifecycleTests` | Valid issue/partial/overdue/final path passed; missing lines, inactive customer, invalid dates, editing/reissue, early overdue, invalid/duplicate/over payment, paid and voided terminal transitions were rejected | PASS |
| V-0404 | 2026-07-31 | Invoice-number allocation is atomic with documented semantics | `ConcurrentAllocatorIsUniqueMonotonicAndTenantIndependent` against PostgreSQL 17.10 | 40 simultaneous independent DbContexts returned distinct `INV-00000001` through `INV-00000040`; a second business independently began at 1 | PASS |
| V-0405 | 2026-07-31 | Optimistic concurrency prevents silent lost invoice updates | `StaleInvoiceWriterReceivesConcurrencyConflict` against PostgreSQL | Two DbContexts loaded one version; first mutation committed and second produced `DbUpdateConcurrencyException` | PASS |
| V-0406 | 2026-07-31 | Database constraints reject a cross-tenant customer reference | `CompositeForeignKeyRejectsCrossTenantCustomer` | Invoice BusinessId paired with another business's CustomerId produced `DbUpdateException` at the composite foreign key | PASS |
| V-0407 | 2026-07-31 | Phase 3 migration applies from both required starting states | `MigrationAppliesFromEmptyAndPhaseTwoSnapshot`; retained Compose database inspection | Latest migration applied to an empty database and after stopping at `20260731083706_AddIdentityAndTenancy`; retained stack lists `20260731093641_AddFinancialDomain` | PASS |
| V-0408 | 2026-07-31 | Earlier product behavior survives the financial schema migration | `scripts/verify-compose.ps1 -Build`; `scripts/verify-phase2.ps1` | Authenticated outbox/Dapr delivery and the full identity/tenant/CSRF/two-replica matrix passed after upgrading the retained Phase 2 database | PASS |
| V-0409 | 2026-07-31 | Phase 3 builds, tests, migrates, and starts from clean repository material | Updated `scripts/verify-clean-copy.ps1` | Copied source ran locked build/scans, all 14 PostgreSQL/domain tests, frontend checks, isolated Compose build on an empty database, Phase 2 matrix, cleanup, and main-stack restoration | PASS |
| V-0499 | 2026-07-31 | Exact Phase 3 gate passes | V-0400 through V-0409; ADR-007/008/009/014 | Concurrent allocation, deterministic finance, lifecycle rejection, and both migration starting states all have executable passing evidence | PASS |
| V-0500 | 2026-07-31 | Current frontend foundation choices are exact-pinned and evidence-based | Official Vue Router, Pinia and TanStack Vue Query documentation; npm registry metadata; `package-lock.json` | Vue Router 5.2.0, Pinia 4.0.2 and Vue Query 5.101.4 were pinned; incompatible jsdom 30 and vulnerable Vue Test Utils were rejected; jsdom 29.1.1 plus direct Vue mounting retained zero audit findings | PASS |
| V-0501 | 2026-07-31 | API failures use a generated, versioned Problem Details contract | `scripts/verify-phase4.ps1`; `contracts/Invoice.Api.json`; nginx at port 8080 | Validation 400 includes field errors; anonymous 401, role 403, and tenant-safe 404 all return `application/problem+json` with status/title/type through the same origin | PASS |
| V-0502 | 2026-07-31 | OpenAPI, generated TypeScript SDK and Zod schemas have zero drift | `scripts/verify-generated.ps1` | Build-time OpenAPI plus Hey API generation preserved aggregate SHA-256 `F15719BBF90B5BA30C99CB321A7FF099C6C31BE094E6DA0EA2D9E62B5AA93661` | PASS |
| V-0503 | 2026-07-31 | Malformed payloads, problem states, error containment and accessibility are tested | `npm test` | 15 tests pass: generated schemas, visibly rejected malformed success payload, 400/401/403/404/409/500 states, root/feature containment, semantic keyboard order, and axe checks | PASS |
| V-0504 | 2026-07-31 | Authentication and shell navigation work from the keyboard in the running app | In-app browser against rebuilt Compose at port 8080 | Enter on the password field authenticated and rendered the workspace; Enter on the Delivery proof link changed the lazy route to `/delivery`; landmarks and labelled controls were present | PASS |
| V-0505 | 2026-07-31 | Lazy-route bundle remains within an explicit budget | `npm run build`; `npm run check:bundle` | 10 JavaScript chunks total 226,113 bytes; largest chunk 151,166 bytes, below 327,680 total and 225,280 per-chunk limits | PASS |
| V-0506 | 2026-07-31 | Locked full verification passes without suppressed failures | `scripts/verify.ps1` | .NET build has 0 warnings/errors; 14 .NET and 15 frontend tests pass; generation/build/budget/Compose config pass; NuGet and npm report no vulnerabilities | PASS |
| V-0507 | 2026-07-31 | Phase 4 preserves real delivery, tenancy, CSRF and replica behavior | `scripts/verify-compose.ps1 -Build`; `scripts/verify-phase2.ps1 -Build` | Outbox/Dapr delivery completed; full role/cross-tenant/CSRF matrix and shared-cookie/shared-antiforgery two-replica checks passed against rebuilt images | PASS |
| V-0508 | 2026-07-31 | Phase 4 operates from clean repository material | `scripts/verify-clean-copy.ps1` | Isolated locked build, all tests/scans, Docker image build, empty database, delivery and full Phase 2 matrix passed; isolated resources were removed and the main stack restored healthy | PASS |
| V-0599 | 2026-07-31 | Exact Phase 4 gate passes | V-0501 through V-0508; ADR-010 | Generated artifacts have zero drift; malformed data is visibly rejected; required error states render; keyboard authentication/navigation pass | PASS |
| V-0600 | 2026-07-31 | Customer semantics were decided before implementation | ADR-011; current EF Core, Npgsql and TanStack primary documentation | Tenant ownership, roles, validation, duplicate create, optimistic concurrency, deletion deferral, escaped search and bounded pagination have explicit alternatives, consequences, failure modes and fallbacks | PASS |
| V-0601 | 2026-07-31 | Customer domain and persistence rules hold against PostgreSQL | `dotnet test BusinessInvoiceTracker.slnx -c Release`; `CustomerTests`; `PostgreSqlDomainTests` | 19 tests pass, including customer normalization/rejection, per-tenant email uniqueness, stale writers, tenant independence, and migrations from empty/Phase 2/Phase 3 snapshots | PASS |
| V-0602 | 2026-07-31 | Customer API authorization, validation, replay, search and paging are executable | `scripts/verify-phase5.ps1` through nginx and real PostgreSQL | 24 retained customers; bounded 10-item pages; exact search; identical replay 200; conflicting replay 409; Viewer mutation 403; cross-tenant read 404; invalid create 400; stale edit 409 | PASS |
| V-0603 | 2026-07-31 | Generated customer response schema matches the live API | First in-app browser customer-list load after the initial Compose build | HTTP 200 payload used `+00:00` timestamps while generated Zod required UTC `Z`; the UI safely rendered its contract-error state | FAIL |
| V-0603-R1 | 2026-07-31 | Generated customer response schema matches the live API | API UTC response normalization; regenerated SDK/Zod; 19 Vitest checks; rebuilt Compose; browser rerun | Live customer pages parse and render; generated artifacts remain deterministic at aggregate SHA-256 `9E8E6DF88C05BCEB93861DA70A64BBC00EAE119E6646457F2186932F207D324E` | PASS |
| V-0604 | 2026-07-31 | Search cancels obsolete work and server pagination is real | `customer-feature.test.ts`; in-app browser page/search journey | The generated fetch client receives the query AbortSignal; the 300 ms debounce cancels obsolete timers; browser Next reached page 2 of 3 and search reset to one server result | PASS |
| V-0605 | 2026-07-31 | Customer create/detail/edit and accessibility work end to end | In-app browser against rebuilt Compose; `customer-feature.test.ts` | Empty submit focused the labelled validation alert; axe found no supported-rule violations; a customer was created, displayed, edited and reloaded from the API | PASS |
| V-0606 | 2026-07-31 | Customer operations are diagnosable without making the default stack fragile | `scripts/verify-compose.ps1 -Observability`; `scripts/verify-phase5.ps1`; structured API logs | Optional viewer received logs, traces and metrics for both .NET services; customer requests carried trace IDs and event 5002 recorded a tenant/customer-scoped update without contact PII | PASS |
| V-0607 | 2026-07-31 | Earlier security, delivery and contract behavior survives Phase 5 | `scripts/verify-phase2.ps1`; `scripts/verify-phase4.ps1`; `scripts/verify-compose.ps1 -Build` | Full tenant/role/CSRF/two-replica matrix, Problem Details matrix, and authenticated outbox/Dapr delivery passed after the customer migration | PASS |
| V-0608 | 2026-07-31 | Phase 5 passes locked, drift, vulnerability, budget and clean-copy gates | `scripts/verify-generated.ps1`; `scripts/verify.ps1`; `scripts/verify-clean-copy.ps1` | Warning-free Release build; 19 .NET and 19 frontend tests; zero reported NuGet/npm vulnerabilities; 247,623-byte lazy bundle; isolated empty-database build/start/test/cleanup passed | PASS |
| V-0699 | 2026-07-31 | Exact Phase 5 gate passes | V-0600 through V-0608; ADR-011 | Every customer implementation-gate item has executable evidence; cross-tenant access is hidden, search is debounced/cancelled, and pagination is bounded and server-connected | PASS |
| V-0700 | 2026-07-31 | Draft command, numbering boundary, replay, audit and conflict semantics precede the slice | ADR-013, ADR-007 amendment, ADR-008/009/014 | Drafts remain unnumbered; ZAR totals come only from the aggregate; stable-ID replay, GUID-version conflicts and transaction-coupled audit have explicit alternatives, failure modes and fallbacks | PASS |
| V-0701 | 2026-07-31 | Financial and replay boundaries pass in the warning-free solution | `scripts/verify.ps1`; `FinancialModelTests` | 20 .NET tests pass, including per-line rounding, invalid inputs, one-to-200 line bounds, replay equivalence despite generated line IDs, and changed-payload detection; Release build has 0 warnings/errors | PASS |
| V-0702 | 2026-07-31 | Tenant-scoped draft API is authoritative, replay-safe and auditable | Two consecutive `scripts/verify-phase6.ps1` runs through nginx/PostgreSQL | Create total 260.00; edit total 345.00; identical replay 200; changed replay 409; Viewer mutation 403; cross-tenant read 404; stale update 409; create/update audits persisted | PASS |
| V-0703 | 2026-07-31 | Invoice OpenAPI, generated client and runtime schemas match live responses | `scripts/verify-generated.ps1`; generated SDK/Zod; live browser/API results | Deterministic aggregate SHA-256 `6A389E9323A5F161D38F58041765540E6C9930239B19423C7B8D426E3F411C79`; UTC `Z` timestamps, nullable draft number, lines and totals parse at runtime | PASS |
| V-0704 | 2026-07-31 | The accessible draft workflow works in the real application | 23 Vitest checks; in-app browser through nginx | Invalid submit focused the labelled alert; two-line create rendered server total R337.65; edit rendered R481.98; preview table and create/update audit history were visible | PASS |
| V-0705 | 2026-07-31 | A stale edit both returns and visibly displays a conflict | API stale-version assertion plus two simultaneous browser editor tabs | Losing API request returned 409; the retained form displayed the `This record changed` alert and `Reload latest` action instead of overwriting | PASS |
| V-0706 | 2026-07-31 | Invoice mutations remain diagnosable without a default viewer dependency | `scripts/verify-compose.ps1 -Observability`; Phase 6 gate; structured API logs | Optional dashboard received logs/traces/metrics for both services; event 6002 recorded business/invoice IDs without customer contact or line-description PII | PASS |
| V-0707 | 2026-07-31 | Earlier security, delivery, contract and customer behavior survives Phase 6 | `scripts/verify-phase2.ps1`; `verify-phase4.ps1`; `verify-phase5.ps1`; rebuilt Compose | Tenant/role/CSRF/two-replica, Problem Details, customer, outbox/Dapr, health and non-root checks all passed after the draft slice | PASS |
| V-0708 | 2026-07-31 | Phase 6 passes locked, drift, vulnerability, budget and clean-copy gates | `scripts/verify.ps1`; `verify-generated.ps1`; updated `verify-clean-copy.ps1` | Fresh copy passed 20 .NET/23 frontend tests, zero reported vulnerabilities, 270,170-byte lazy bundle, empty-database Compose, and Phase 2/4/5/6 gates; isolated resources were removed and the main stack restored healthy | PASS |
| V-0799 | 2026-07-31 | Exact Phase 6 gate passes | V-0700 through V-0708; ADR-013 | Server totals, edge cases, replay, tenant isolation, PostgreSQL persistence, visible concurrency conflict, audit and complete draft E2E all have runnable evidence | PASS |
| V-0800 | 2026-07-31 | Lifecycle command semantics precede implementation | ADR-015; ADR-004/007/009 | Issue, payment and void identities, authorization, atomic boundaries, audit/outbox ownership, failure modes and fallbacks are explicit | PASS |
| V-0801 | 2026-07-31 | Lifecycle implementation is warning-free and contract-connected | `dotnet build`; `dotnet test`; `npm test`; `npm run build` | 0 warnings/errors, 20 .NET tests and 24 frontend tests pass; lifecycle requests are generated from OpenAPI and parsed by generated Zod schemas | PASS |
| V-0802 | 2026-07-31 | Concurrent issue and payment behavior is bounded and replay-safe | Three independent `scripts/verify-phase7.ps1` runs through nginx/PostgreSQL | Each run allocated distinct invoice numbers; exact issue/payment/void replay returned 200; changed identity reuse returned 409; payment race returned one 200 and one 409; overpayment returned 400 | PASS |
| V-0803 | 2026-07-31 | Every lifecycle and authorization path is proven | `scripts/verify-phase7.ps1`; audit and tenant assertions | Draft to Issued to PartiallyPaid to Paid and Draft to Voided passed; paid invoice void failed; Viewer mutations returned 403; cross-tenant lookup returned 404; audit actions persisted | PASS |
| V-0804 | 2026-07-31 | Lifecycle event writes remain atomic and await the correct delivery owner | Phase 7 PostgreSQL transaction implementation; outbox SQL assertion | Six expected issue/payment/void messages remained pending with stable command IDs; the Phase 1 publisher filters them until Phase 8 adds their consumer | PASS |
| V-0805 | 2026-07-31 | Generated artifacts, supply-chain scans and frontend budget remain sound | `verify-generated.ps1`; `verify.ps1` | Deterministic aggregate SHA-256 `AEC83471EBE68804D08F998DB02A97A6179AFB195E0B5E1855BA98A8F002ACE0`; zero reported NuGet/npm vulnerabilities; 274,233-byte lazy bundle | PASS |
| V-0806 | 2026-07-31 | Earlier security, contract, customer and draft behavior survives Phase 7 | Phase 2/4/5/6 regression scripts; `verify-compose.ps1 -Observability` | Replica cookie, CSRF, role/tenant, Problem Details, customer, invoice, delivery, health and non-root gates pass; both .NET services emit logs/traces/metrics | PASS |
| V-0807 | 2026-07-31 | Phase 7 passes from copied source and an empty database | Updated `scripts/verify-clean-copy.ps1` | 393-second run passed locked build/test/scans, fresh Docker build/migration, Phases 2/4/5/6/7, guarded cleanup and main-stack restoration | PASS |
| V-0808 | 2026-07-31 | Automated visible-browser control is available for a lifecycle rerun | In-app browser skill bootstrap and prescribed recovery | Control runtime could not create its kernel assets; no browser claim is made. This is not a listed Phase 7 gate condition; component/runtime-contract tests plus nginx API E2E remain the retained evidence | BLOCKED (tooling) |
| V-0899 | 2026-07-31 | Exact Phase 7 gate passes | V-0800 through V-0807; ADR-015 | Concurrent allocation, duplicate commands, non-overpaying concurrent payments, every transition and authorization path have executable evidence; V-0808 is explicitly excluded from the claim | PASS |
| V-0900 | 2026-07-31 | Phase 8 failure boundaries and retry ownership are decided before behavior | Current Dapr/Mailpit primary documentation; ADR-016 | Stable event/workflow/delivery IDs, inbox plus durable command, database re-read, retry counts, Mailpit crash-window reconciliation, dead letter and Hangfire fallback are explicit | PASS |
| V-0901 | 2026-07-31 | Exact-pinned Phase 8 runtime prerequisites initialize together | Release build; targeted Compose build/up; sidecar logs; PostgreSQL query; HTTP readiness | Dapr Workflow 1.17.9 builds with 0 warnings; runtime 1.17.7 loads environment secret store and PostgreSQL v2 workflow state, connects scheduler/placement, creates workflow tables; pinned Mailpit and worker return 200 ready | PASS |
| V-0902 | 2026-08-01 | Lifecycle handoff and notification persistence compile and migrate without weakening prior behavior | Analyzer-enabled solution build; EF migrations from empty and prior snapshots; `.NET` and Vue suites | Three reviewed Phase 8 migrations apply; build has 0 warnings/errors; 20 .NET and 24 Vue tests pass | PASS |
| V-0903 | 2026-08-01 | Retained lifecycle outbox rows reach a durable idempotent workflow boundary | Recreated API/automation sidecars; Dapr subscription logs; PostgreSQL outbox/inbox/command queries; draft-void recovery | All 23 retained lifecycle messages published; 23 workflow commands completed with no failed row; ten issued workflows started; thirteen signals completed; three never-issued draft voids became explicit logged no-ops | PASS |
| V-0904 | 2026-08-01 | An already-due invoice becomes overdue and receives one simulated notification through the real stack | `scripts/verify-phase8-happy-path.ps1`; nginx/API/Dapr/PostgreSQL/Mailpit | `INV-00000014` became Overdue; exactly one delivery, attempt, audit and overdue outbox row persisted; Mailpit returned the normalized stable MessageID; build and regression rerun remained clean | PASS |
| V-0905 | 2026-08-01 | Duplicate lifecycle delivery is absorbed before workflow side effects | Extended `scripts/verify-phase8-happy-path.ps1`; two direct CloudEvent replays; PostgreSQL cardinality query | `INV-00000016` completed the same overdue/Mailpit path; two duplicate `InvoiceIssued` replays returned the explicit duplicate acknowledgement and left exactly one inbox row and one workflow command | PASS |
| V-0906 | 2026-08-01 | Durable timer recovery and due-date change rescheduling work in product code | `scripts/verify-phase8-reschedule.ps1`; paired automation/sidecar stop/start; API replay; PostgreSQL evidence | Worker restarted during the timer; due-date command and exact replay remained consistent; invoice `412ff119-e0f6-452d-b00a-1366e04063fc` became Overdue with one audit and one delivered notification | PASS |
| V-0907 | 2026-08-01 | Notification retry has one bounded owner, survives restart and exposes terminal/manual recovery | `scripts/verify-phase8-notification-recovery.ps1`; Mailpit 451/550 chaos; paired worker replacement; database attempts | Transient case delivered on attempt 2 after restart; permanent case stopped after one attempt and eight-second no-retry window; one durable manual cycle delivered on total attempt 2 | PASS |
| V-0908 | 2026-08-01 | Terminal events before wait and racing the due activity cannot send a notification | `scripts/verify-phase8-terminal-races.ps1`; stopped worker; injected transient window; paid/void API commands | Paid-before-wait ended with zero delivery; paid and void races persisted Cancelled delivery after one attempt; Mailpit contained zero terminal-race messages | PASS |
| V-0909 | 2026-08-01 | Workflow and provider state are reconcilable from PostgreSQL authority | `scripts/verify-phase8-reconciliation.ps1`; supported Dapr terminate/purge API; simulated Mailpit accepted-before-ack window | Purged active workflow was recreated by a durable command; accepted message became Delivered with one Reconciled attempt and remained one Mailpit message; a separately lost due event was reasserted and completed | PASS |
| V-0910 | 2026-08-01 | Two current publishers cannot concurrently own or amplify one outbox row | `scripts/verify-phase8-two-publishers.ps1`; fresh app/sidecar pair recreation; 100-row marker batch | Publishers split claims 40/60; all 100 rows published with exactly one attempt, maximum one attempt and exactly 100 inbox side effects | PASS |
| V-0911 | 2026-08-01 | Phase 8 preserves full build, test, generation, audit and bundle gates | `scripts/verify.ps1`; `scripts/verify-generated.ps1` | Release build 0 warnings/errors; 21 .NET and 24 Vue tests; zero reported NuGet/npm vulnerabilities; deterministic generated hash `8EAA...5F7`; bundle 274,310 bytes, largest 98,668 | PASS |
| V-0912 | 2026-08-01 | The complete stack and all Phase 2-8 gates work from a clean directory and fresh volumes | `scripts/verify-clean-copy.ps1` | Final-source copy `business-invoice-phase2-clean-64421fde3b9c47c8b173a083878b78e2` built, migrated, ran every retained phase and every Phase 8 script, then removed its containers/volumes; main stack restoration completed | PASS |
| V-0913 | 2026-08-01 | Exact Phase 8 gate passes | V-0900 through V-0912; ADR-016; requirements and risks reconciled | All required restart/event/race/retry/duplicate/reconciliation/multi-publisher/clean-copy proofs pass and no success precedes durable state | PASS |
| V-1000 | 2026-08-03 | Phase 9 query and UI decisions precede implementation | Current EF Core pagination, WCAG 2.2/status-message/focus-order and TanStack Vue Query primary documentation; ADR-017 | Allow-listed filters/sorts, unique ordering, bounded offset pages, server aggregation, per-currency totals, fixed list limits, native controls and the no-chart decision are explicit with failure modes and fallbacks | PASS |
| V-1001 | 2026-08-03 | Invoice discovery and dashboard queries are bounded, deterministic and tenant-safe | `scripts/verify-phase9.ps1`; nginx/API/PostgreSQL; two tenants; cross-currency fixture | Four marker invoices were found; status/search/order/pages behaved deterministically; five invalid queries returned 400; other-tenant access returned 404; ZAR 295.00 and USD 57.50 stayed separate; lists stopped at 5/10 and both query indexes existed | PASS |
| V-1002 | 2026-08-03 | Phase 9 frontend controls, states, accessibility, responsive reflow and budget are executable | 26 Vitest checks including axe and rendered feature behavior; live in-app browser; `npm run build`; `npm run check:bundle` | Dashboard and routed overdue filter rendered through nginx; controls were labelled and functional; result status announced 30 rows; 375px viewport had no horizontal overflow and reflowed to one/two columns; 25 chunks total 283,505 bytes, largest 103,266 | PASS |
| V-1003 | 2026-08-03 | Generated dashboard runtime contract matches live values | First rebuilt dashboard browser load | A deterministic audit `Guid` without an RFC version nibble was rejected by generated `z.uuid()` and the UI safely rendered its contract-error state | FAIL |
| V-1003-R1 | 2026-08-03 | Generated dashboard runtime contract matches live values | Expose opaque activity ID as a string; rebuild OpenAPI/SDK/Zod and images; browser reload; `verify-generated.ps1` | Live dashboard parsed and rendered all sections; generated artifacts are deterministic at `750FD3E5FC59F9C00769EE1362401387A0B5CC959E841B8D1401AD538FABB5A0` | PASS |
| V-1004 | 2026-08-03 | Phase 9 and every retained prior gate pass from clean repository material | Updated `scripts/verify-clean-copy.ps1` | Fresh copy `business-invoice-phase2-clean-0207e7bc6942473aa97eeef917247bf2` built with 0 warnings, passed 21 .NET and 26 Vue tests/scans/budget, migrated fresh volumes, ran Phases 2 and 4-9, removed isolated resources and restored the main stack | PASS |
| V-1005 | 2026-08-03 | The Phase 9 manual keyboard script passes | In-app browser control against the rebuilt stack | The page exposed the expected semantic focusable controls, but this browser-control session did not deliver native Tab/Enter events after focus. Automated focus/accessibility evidence is retained, but no manual keyboard pass is inferred | BLOCKED (tooling/manual confirmation) |
| V-1099 | 2026-08-03 | Exact Phase 9 gate passes | V-1000 through V-1005; ADR-017 | Query bounds, controls, automated accessibility, responsive behavior, contracts, budget and clean-copy execution pass; the required manual keyboard script remains unconfirmed | BLOCKED |
| V-1005-R1 | 2026-08-03 | The Phase 9 manual keyboard script passes | User execution of `docs/evidence/phase-9-keyboard-script.md` against the running application | User explicitly reported “Phase 9 keyboard passed”; browser version and exact time were not captured and are not inferred | PASS |
| V-1099-R1 | 2026-08-03 | Exact Phase 9 gate passes | V-1000 through V-1004, V-1005-R1, ADR-017, reconciled risks and requirements | Bounded queries, tenant isolation, functional controls, automated accessibility, manual keyboard operation, responsive behavior, strict contracts, bundle budget and clean-copy execution all pass | PASS |
| V-1100 | 2026-08-03 | Phase 10 resilience and diagnostic policy precedes implementation | Current primary ASP.NET Core 10 health, Dapr 1.17 health/resiliency, OpenTelemetry .NET, .NET metrics/HTTP/SMTP and Npgsql documentation; ADR-018; resilience matrix | Required/degraded dependencies, fixed timeouts, one retry owner per boundary, low-cardinality telemetry, cached operator state, failure modes and fallbacks are explicit | PASS |
| V-1101 | 2026-08-03 | Runtime changes compile, test, generate and scan cleanly | `scripts/verify.ps1` | Locked Release build had 0 warnings/errors; 21 .NET and 26 Vue tests passed; OpenAPI regenerated; production bundle stayed at 283,505 bytes; NuGet/npm reported no known vulnerabilities at configured thresholds | PASS |
| V-1102 | 2026-08-03 | Required process and dependency failures have bounded, truthful behavior and recovery | `scripts/verify-phase10.ps1`; retained Compose stack | API/automation Dapr, placement, scheduler and PostgreSQL produced liveness 200/readiness 503; Mailpit produced ready/degraded; API loss left static web 200/API 502; automation and web loss were isolated; PostgreSQL recovered in the same process containers; Redis backlog was visible and recovered at attempts 2 then 3; default topology was restored | PASS |
| V-1103 | 2026-08-03 | Notification failure is bounded and diagnosable from trace/workflow/delivery identity | `verify-phase8-notification-recovery.ps1`; `verify-phase10-notification-diagnostics.ps1`; JSON logs and PostgreSQL | Trace `27870f69f683cc46d12902d81720ab72` identified workflow `invoice-2fcfa3282b5b43c9ad3b3e77c8d3395b-overdue-v1` and delivery `a0e1923d-438a-9e80-8eb6-19735f465e74`; one durable permanent failure and one explicit successful manual retry were found; fresh-copy correlation also passed with independent IDs | PASS |
| V-1104 | 2026-08-03 | Backlogs/failures and all three telemetry signals are operator-visible without a required viewer | `/internal/operations/status`; cached gauges; Phase 10 runbook; `verify-compose.ps1 -Observability`; observer-stop assertion | Snapshot exposed outbox count/age, pending/failed commands, dead letters and pending/failed notifications with staleness; both services exported logs/traces/metrics; stopping Aspire left both services ready | PASS |
| V-1105 | 2026-08-03 | Phase 10 and all retained gates pass unattended from clean repository material and fresh volumes | Corrected `scripts/verify-clean-copy.ps1` | Fresh copy `business-invoice-phase2-clean-35b60003ef9040e9a815f0b04b8f8749` passed locked build/tests/scans/budget, all three ordered API/sidecar resets, Phases 2 and 4-10, clean notification correlation and process/dependency recovery; it removed its containers/volumes/copy and restored the main stack without intervention | PASS |
| V-1199 | 2026-08-03 | Exact Phase 10 gate passes | V-1100 through V-1105; ADR-018; resilience matrix; operations runbook | Every in-scope dependency/process failure has tested behavior, attempts do not amplify, notification failure is correlated from one trace/workflow/delivery identity, and durable backlog/failure inventories are operator-visible | PASS |
| V-1200 | 2026-08-04 | Phase 11 trust decisions precede implementation | Current primary sources; threat model; ADR-019; Phase 11 working plan | Trust boundaries, migration ownership, scan scope, alternatives, consequences, failure modes and fallbacks are explicit | PASS |
| V-1201 | 2026-08-04 | Locked regression, formatting, model and dependency gates are clean | `scripts/verify.ps1`; `dotnet format --verify-no-changes`; EF pending-model check | Release build 0 warnings/errors; 21 .NET tests; 26 Vue tests; coverage 35.74/33.96/25.27/38.37 met configured floors; generated artifacts/model had zero drift; NuGet/npm audits and bundle budget passed | PASS |
| V-1202 | 2026-08-04 | Reviewed same-origin security boundaries resist the tested abuse cases | `scripts/verify-phase11-security.ps1`; Phase 2 matrix; threat model | Headers/cookies passed; missing CSRF 400; TRACE 405; nine admitted bad logins followed by three 429 responses; tenant/role/replica assertions remained passing | PASS |
| V-1203 | 2026-08-04 | Migration deployment is reviewable, ordered and repeat-safe | `scripts/verify-phase11-migrations.ps1`; ADR-019; delivery runbook | Pending model: none; idempotent SQL 32,779 bytes SHA-256 `9DED9C0D...B572B7DE`; destructive guard clean; earlier snapshot test passed; two one-shot migrator reruns preserved inventory `10|6|2` and readiness 200 | PASS |
| V-1204 | 2026-08-04 | Representative authenticated read profile meets its diagnostic bound | `scripts/verify-phase11-performance.ps1` | Clean-copy run: 120 requests at concurrency 8, zero errors, p50 16 ms, p95 167 ms, max 316 ms; this is a local diagnostic, not an SLO or scalability claim | PASS |
| V-1205 | 2026-08-04 | Graceful stop and restart preserve durable asynchronous work | `scripts/verify-phase11-shutdown.ps1` | Twelve rows were attempted before stop; paired stop completed in 0.82 s with exit 0; restart recovered 12/12 with 12 receipts and maximum two attempts | PASS |
| V-1206 | 2026-08-04 | Repository and final container artifacts pass scoped supply-chain gates | `scripts/verify-phase11-supply-chain.ps1`; immutable Gitleaks and Docker Scout inputs | Clean copyable source had no detected leaks; API/automation/web final images ran as 1654/1654/101; no critical/high vulnerable package was detected in the final images; SPDX SBOMs contained 160/169/87 packages | PASS |
| V-1207 | 2026-08-04 | CI policy mirrors the locally passing delivery gates | `.github/workflows/ci.yml`; YAML parse; full-SHA/action and container-digest policy checks; local scripts | Least-privilege workflow runs formatting, drift, tests/coverage, audits, scans, SBOM and Compose; all action pins are full SHAs. Remote hosted-runner execution remains unobserved as R-020 | PASS (local policy) |
| V-1208 | 2026-08-04 | Phase 11 and all retained gates pass from clean repository material | `scripts/verify-clean-copy.ps1` | Fresh copy `business-invoice-phase2-clean-042550e08000436cb3351ca02be4dbc2` passed locked build, 21 .NET/26 Vue tests, Phases 2 and 4-11, final-image scans/SBOMs, isolated cleanup and main-stack restoration | PASS |
| V-1209 | 2026-08-04 | Backup/restore, role separation and current migration compatibility are executable | `scripts/verify-phase11-database-operations.ps1` | Custom backup restored equal inventory `10|6|2|29|76|19|365|207`; application DML passed before/after the index-only latest migration; migrator schema changes passed; application schema creation was denied; main readiness stayed 200; zero temporary databases/roles remained | PASS |
| V-1299 | 2026-08-04 | Exact Phase 11 gate passes | V-1200 through V-1208; ADR-019; threat model; delivery runbook | No critical/high untriaged finding or detected source secret; build/run/test instructions passed clean copy; measured performance met the declared diagnostic bound; graceful shutdown recovered all durable work | PASS |
| V-1210 | 2026-08-04 | The extended database deployment proof passes from untouched copied source and fresh volumes | `scripts/verify-clean-copy.ps1` | Fresh copy `business-invoice-phase2-clean-827baaad38734482b156dd08cf5db249` passed 21 .NET/26 Vue tests and every retained Phase 2-11 gate; restored inventory `10|6|2|24|17|6|131|47` matched; role separation and compatibility passed; final images scanned clean at critical/high; isolated resources were removed and the main stack restored | PASS |
| V-1299-R1 | 2026-08-04 | Exact Phase 11 gate remains passing after the pre-Phase-12 deployment extension | V-1200 through V-1210; expanded focused suite and clean-copy rerun | The original security/performance/shutdown/supply-chain conditions remain passing, and backup/restore inventory, separate migrator/application permissions and exact current-migration compatibility now have both retained-stack and fresh-stack evidence | PASS |

## Current verified command summary

```text
Repository-local .NET SDK: 10.0.302
ASP.NET Core runtime: 10.0.10
Dapr .NET SDK/Workflow package: 1.17.9
Candidate Dapr runtime: 1.17.7
Node.js: v24.14.1
npm: 11.11.0
Docker Desktop: 4.84.0
Docker Engine: 29.6.2
Docker Compose: v5.3.1

Auth local:
PASS anonymous=401 no-redirect
PASS cross-replica-cookie=200
PASS missing-csrf=400
PASS cross-replica-antiforgery=200
PASS wrong-role=403

Contract:
PASS two warning-as-error Release builds
PASS locked NuGet restores
PASS one Vitest file / seven tests
PASS Vue production build
PASS npm audit and both NuGet vulnerability scans
PASS PowerShell syntax and Compose YAML parsing
PASS same-origin nginx cookie/antiforgery integration
PASS Dapr buffered external event
PASS Dapr durable timer after worker and sidecar restart
PASS PostgreSQL v2 workflow actor state
PASS optional dashboard received logs, traces and metrics
PASS final clean-copy build/start/test/cleanup
PASS 20 .NET domain/PostgreSQL tests
PASS 24 frontend tests
PASS 40-way concurrent per-business invoice-number allocation
PASS genuine stale-writer concurrency conflict
PASS empty and Phase 2 snapshot migration paths
PASS database-level cross-tenant customer rejection
```

## Material failed attempts retained

1. Global .NET 9 could not satisfy `global.json`; the official installer placed
   SDK 10.0.302 under `.tools/dotnet`, and commands explicitly set `DOTNET_ROOT`.
2. Build-time OpenAPI initially launched its child process with global .NET; the
   process-local PATH fix made generation repeatable.
3. TypeScript 7.0.2 caused Hey API 0.99.0 to fail on `SyntaxKind.AnyKeyword`;
   exact-pinned TypeScript 6.0.3 passes.
4. Hey API's generated fetch runtime did not compile with
   `exactOptionalPropertyTypes`; that optional extra flag was removed while
   `strict` and `noUncheckedIndexedAccess` remain.
5. npm audit found js-yaml below 4.3.0 and NuGet reported Microsoft.OpenApi below
   2.7.5; both were pinned to patched releases and rescanned.
6. Numeric JSON strings initially passed the generated integer schema; strict
   server JSON number handling corrected the contract.
7. An all-ones UUID fixture was not RFC-valid and Zod correctly rejected it; the
   fixture now uses an RFC-compatible version/variant.
8. The first login request was malformed by command-line quoting; request bodies
   now use temporary JSON files.
9. An unhandled antiforgery exception initially returned 500; the endpoint filter
   now fails closed with RFC Problem status 400.
10. Starting two processes simultaneously against an empty key directory produced
    competing initial keys. The spike initializes the shared ring before the
    second replica. Safe concurrent first-deploy bootstrap remains documented.
11. The first OpenTelemetry unified-exporter API call did not compile in 1.17.0;
    supported per-signal exporters now compile.
12. The proposed .NET 10 `bookworm-slim` images do not exist because .NET 10
    stopped shipping Debian images. Digest-pinned Ubuntu Noble images replaced
    them before runtime execution.
13. A first hash-report one-liner had a PowerShell parser error; the corrected
    command proved both governance copies byte-identical.
14. The clean-copy gate was executed and failed because `docker` is not installed.
    The temporary copy was removed by guarded cleanup.
15. Docker Desktop installation initially remained unusable until firmware
    virtualization was enabled. After reboot, Windows reported a hypervisor and
    `VirtualizationFirmwareEnabled=True`; Engine and Compose then responded.
16. The first containerized antiforgery request returned 500 because the non-root
    API user could not write `/keys`. The image now creates and owns that exact
    directory; the same-origin test passes.
17. A clean copy initially imported host `obj` files containing Windows package
    paths into Linux builds. Per-context `.dockerignore` files now exclude build
    output, and the clean-copy build passes.
18. Separate app/sidecar DNS names produced a startup race. The topology now uses
    Dapr's documented shared app network namespace and localhost ports.
19. Restarting the shared-namespace app and sidecar in parallel was nondeterministic.
    The test now stops both, starts and probes the app, then starts and probes Dapr.
20. Redis passed the workflow tests but Dapr logged that it lacks actor transaction
    rollback and should not be used that way in production. Workflow actor state
    moved to the stable PostgreSQL v2 component; Redis remains Pub/Sub only.
21. The Aspire root redirects to `/structuredlogs`, so the original HTTP-200
    readiness assertion failed despite a healthy dashboard. The gate now checks
    the explicit page and requires the telemetry API to report all three signals
    for both .NET services.
22. The first product migration process exited 139 because build-only data
    protection tried to use an unwritable path. Migration mode now uses ephemeral
    protection and exits after applying migrations.
23. The anonymous antiforgery token became invalid after login changed the
    principal. The client and verifier now fetch a fresh token after login.
24. The first worker transaction failed because Npgsql retry strategy cannot
    implicitly replay a user transaction. The whole inbox-plus-receipt unit now
    runs through EF's execution strategy and passes.
25. The first generated date-time runtime test rejected ASP.NET's `+00:00`
    representation. The API contract now returns UTC `DateTime` values serialized
    with `Z`; the regenerated Zod schema test and live response pass.
26. PowerShell did not stop automatically after the failed Vitest native command.
    `scripts/verify.ps1` now checks every native exit code explicitly.
27. A sidecar replacement left a Redis Stream message pending until the default
    60-second redelivery scan. Documented 10-second processing and 2-second scan
    settings now recover within the bounded integration gate; inbox deduplication
    protects repeats.
28. nginx cached a replaced API container's IP and routed to an unrelated reused
    address. Docker DNS is now re-resolved every five seconds; the observability
    restart and subsequent E2E pass.
29. Npgsql 10 probed for absent Kerberos libraries and printed a documented,
    harmless error. Local password-only Compose connections now explicitly set
    `GSS Encryption Mode=Disable`.
30. After reboot, Windows resolved only SDK 9.0.101 although the repository
    remained correctly pinned to 10.0.302. The exact SDK was reinstalled from the
    Windows package source; locked build V-0300 passes without changing the pin.
31. The first Phase 2 Release build failed CA1725 because the Identity base
    context names its model-builder parameter `builder`. The override now matches
    the base contract; no analyzer was suppressed.
32. The generated Identity migration initially failed CA1861 for a repeated
    index-column array. A named static field replaced the allocation; the
    migration and warning-as-error build pass.
33. The first authorization matrix reached the Owner membership endpoint and
    returned 500 because EF Core could not translate ordering after DTO
    construction. Ordering now occurs on the joined database field before
    projection; V-0304-R1 retains the failure and proves the fix.
34. The first missing-secret assertion incorrectly expected an empty process
    variable to override the explicit Compose environment file. A fixture that
    genuinely omits the variable now proves interpolation fails closed.
35. The first Production seed assertion used an unsupported Compose
    `--environment` spelling. The documented `--env` option now launches the
    product migrator and proves the seeder itself rejects Production.
36. The hand-authored xUnit v3 project initially omitted the explicit global
    `Xunit` import, so test attributes were not resolved. `Usings.cs` now declares
    it and the test assembly builds.
37. The first fixture used xUnit v2-style `Task` lifecycle signatures. xUnit v3
    requires `ValueTask`; the fixture now implements the current contract.
38. Warning-as-error analysis rejected the fixture type suffix and async test
    calls without the current test cancellation token. The group was renamed and
    every cancellable database operation now receives
    `TestContext.Current.CancellationToken`; no analyzer was suppressed.
39. EF generated constant arrays in the Phase 3 migration that violated CA1861.
    Named static column arrays replaced them, as in the earlier Identity
    migration, and the warning-as-error build passes.
40. The initial migration was regenerated after adding a database-level invoice
    status/amount consistency constraint. No released or applied schema was
    rewritten; both required migration starting points were then rerun.
41. A diagnostic `psql -c` command lost quoted identifiers through native
    PowerShell argument parsing and reported a nonexistent lowercase table. The
    corrected stdin query listed all four applied migrations, including
    `20260731093641_AddFinancialDomain`.
42. Running Vitest while contract generation cleaned and rewrote the generated
    directory produced a false missing-module failure. Generation and consumers
    now run sequentially because they share mutable artifacts.
43. The first live customer page rejected otherwise valid API data because
    `DateTimeOffset` serialized UTC as `+00:00` while the generated Zod schema
    required `Z`. The API response boundary now emits UTC `DateTime`; regeneration,
    focused tests and the browser rerun pass.
44. The first repeat of `verify-phase5.ps1` correctly returned 409 because its
    own stale-write scenario had left fixture 2 edited. The script now restores
    that known fixture before replaying create assertions and repeated runs pass.
45. The first Phase 6 line replacement attached application-ID lines to a tracked
    aggregate as updates, so PostgreSQL reported a zero-row concurrency conflict.
    The endpoint now marks replacement lines as inserts while EF orphan handling
    deletes the prior collection; create/edit and repeat gates pass.
46. The first audit read returned 500 because EF could not translate a
    `DateTimeOffset.UtcDateTime` conversion inside its SQL projection. Audit and
    list queries now materialize bounded database rows before UTC response mapping.
47. The first valid browser draft submit never reached nginx because
    `structuredClone` cannot clone Vue's reactive Proxy. The form now copies its
    explicit typed shape, a rendered-component regression test covers submit, and
    the complete browser create/edit journey passes.
48. The first generated-drift rerun failed after new documented invoice HTTP
    responses intentionally changed OpenAPI. The generated SDK/Zod files were
    reviewed and the deterministic rerun passes at the V-0703 hash.
49. Running security phase scripts back-to-back consumed the intentional
    ten-per-minute in-memory login limiter and Phase 5 received 429. Independent
    clean-copy phase gates now restart only the API between the security group and
    product slices; the limit is unchanged and all gates pass deterministically.
50. The first expanded clean-copy run misread an expected Production-seed native
    exit 139 retained inside a successful PowerShell Phase 2 script. The wrapper
    now distinguishes PowerShell-script success from native command exit codes;
    the full clean-copy rerun passes and cleanup remains guarded.
51. The first lifecycle build exposed execution-strategy delegate inference, a
    missing event namespace and CA1859. Explicit `ExecuteAsync<IResult>`, the
    correct namespace and a concrete validation return type fixed all three;
    no analyzer was suppressed.
52. A full Compose rebuild exceeded the outer 240-second command timebox after
    images and healthy services completed. The runtime-only verifier then passed;
    the later 393-second clean-copy gate retained the complete cold-build proof.
53. The first Phase 7 script could not resolve `HttpClient`; explicitly loading
    the framework assembly made the concurrent request harness portable here.
54. Copying a textual Cookie header into concurrent clients returned 401. A
    shared `CookieContainer` preserved the authenticated session and the race ran.
55. EF initially classified the domain-created application-GUID Payment as an
    update and returned a false concurrency conflict. Marking that new payment
    Added mirrors the proven invoice-line rule and real PostgreSQL payment passes.
56. An exact payment replay initially conflicted because PostgreSQL stores
    microseconds while .NET accepted finer ticks. The command timestamp is now
    normalized once to database precision for persistence and comparison; exact
    replay passes and changed replay still conflicts.
57. The in-app browser control runtime could not create its kernel assets and its
    prescribed recovery call failed identically. No visible-browser evidence is
    claimed for Phase 7; the tooling limitation is retained as V-0808.
58. A convenience component-YAML check called unavailable PowerShell
    `ConvertFrom-Yaml` and misleadingly continued. It is not evidence. Compose
    parsing passed, and actual Dapr 1.17.7 initialization then loaded all three
    components and created the PostgreSQL workflow tables as V-0901.
59. The first Phase 8 migration command had no design-time connection setting.
    A non-secret inert design connection was supplied explicitly; generation then
    passed without contacting a database.
60. Initial Phase 8 compilation exposed a moved-contract namespace error, an
    anonymous nullable-type mismatch and CA1861 in generated migration arrays.
    The types and generated constants were corrected; no warning was suppressed.
61. A combined .NET/frontend regression invocation failed to terminate and
    yielded no trustworthy result. It was stopped and both suites were rerun
    independently to 20 and 24 passing tests.
62. Migration tests correctly failed because their expected latest migration was
    stale after each Phase 8 schema addition. Every supported starting-point
    assertion now targets the reviewed latest migration and passes.
63. Retained draft-void signals initially exhausted retries because no workflow
    should exist for a never-issued invoice. The payload's invariant (`Voided`
    with no invoice number) now produces a structured no-op; all three rows were
    narrowly requeued and recovered with no failed commands.
64. Recreating only an app that shares its network namespace with a Dapr sidecar
    caused temporary localhost connection refusal. Compose recovery must recreate
    the app-sidecar pair together; the pair reconnected to durable workflow state.
65. The first happy-path verifier reused the anonymous antiforgery token after
    login and received the intended 400. It now refreshes the authenticated token.
66. One Docker image was built while an earlier multi-file patch had not applied
    the workflow activity branch. The timer-only instance truthfully remained
    Issued; source verification, a clean rebuild and a new instance proved the
    actual activity path.
67. The first delivery assertion raced the asynchronous activity and observed its
    correct Pending/zero-attempt intermediate state. A bounded terminal-state poll
    replaced the instantaneous assertion.
68. Mailpit normalizes RFC Message-ID by omitting angle brackets in its API. Both
    reconciliation and verification now compare the normalized value; the stable
    identifier is present and V-0904 passes.
69. The duplicate verifier initially expected HTTP 200, while the subscriber
    intentionally acknowledges an already-processed event with 204. The verifier
    now accepts both success forms and proves database cardinality remains one.
70. Migration tests correctly rejected a stale expected-latest migration after
    the notification retry-cycle schema was added. Every supported starting point
    now targets `20260801171347_AddNotificationRetryCycle`; 21 tests pass.
71. The retained automation container started against a database missing the new
    retry-cycle column because the migrate image itself was stale. Explicitly
    building and running the current migrate image before recreating the worker
    applied the reviewed migration; the failure was not suppressed.
72. Final due-date verification exposed a stale API replica claiming new lifecycle
    types and publishing them to `walking-skeleton`. Redis preserved the wrong
    topic and `invoice-api-replica` source while PostgreSQL truthfully showed the
    rows as published. Both publisher pairs are now recreated from the current
    image for concurrency tests, publication logs include type/topic, and the
    authoritative per-version reconciler repaired the stranded invoice.
73. The first two-publisher fixture serialized PascalCase properties, producing
    invalid walking-skeleton input and redelivery noise. The fixture now emits the
    real camelCase contract, and the subscriber safely acknowledges/logs malformed
    historical probes rather than starving valid lifecycle work.
74. The updated happy-path verifier used C-style escaped quotes in a PowerShell
    SQL string and failed parsing before execution. PowerShell backtick escaping
    fixed the script; the exact original outbox payload now drives two successful
    duplicate replays.
75. The first expanded clean-copy Phase 7 check raced the live Phase 8 publisher:
    four of six atomic outbox rows were still pending when the query expected all
    six pending. The requirement is atomic row creation, not scheduler timing, so
    the assertion now counts the six stable command IDs regardless of later
    publication. The complete fresh-directory/fresh-volume rerun passes V-0912.
76. A final build and test were mistakenly launched concurrently against the
    same output directory, so the test host locked an assembly and the build
    failed with copy retries. They were rerun sequentially without changing code:
    the build returned zero warnings/errors and all 21 .NET tests passed.
77. A fresh-volume reschedule proof observed the atomic Overdue/audit commit
    before the separate notification workflow completed, so its immediate query
    found zero Delivered rows. The verifier now uses a bounded terminal-delivery
    poll while retaining exact cardinality checks; it does not conflate the two
    durability boundaries.
78. The first Phase 9 migration generation used the wrong case-sensitive
    connection-string environment key, then generated CA1861-triggering inline
    column arrays. The correct key and named static arrays produced a clean
    warning-as-error migration without suppressions.
79. The first Phase 9 cross-currency verifier attempted a USD write through the
    intentionally ZAR-only Phase 6 command boundary and correctly received 400.
    A narrow SQL read-model fixture now proves currency separation without
    broadening product write scope.
80. The first filtered invoice query projected into a record before applying
    provider-specific `ILIKE`; EF could not translate it and returned 500. Tenant
    filtering and search now occur on the database join before sorting/projection,
    and repeated nginx/PostgreSQL verification passes.
81. The first live dashboard load safely rejected an activity identifier because
    a deterministic .NET `Guid` is not necessarily an RFC-versioned UUID while
    generated Zod enforced the advertised format. Activity IDs are now exposed as
    opaque strings; runtime validation remains strict and the rebuilt page passes.
82. The in-app browser exposed correct semantic controls but did not deliver
    native Tab/Enter events from either documented keyboard API in this session.
    No manual pass is claimed; V-1005 and the exact Phase 9 gate remain blocked
    pending the short human keyboard script. The user subsequently executed that
    script and explicitly reported it passed; V-1005-R1 and V-1099-R1 retain the
    resolution without erasing the original tooling limitation.
83. A large initial Phase 10 health patch returned truncated tool output. Source
    markers were inspected and a full warning-as-error build passed before any
    further change; no application was inferred from the tool response alone.
84. The first custom metric build found ambiguous .NET 10 single-tag overloads.
    Explicit `KeyValuePair<string, object?>` values removed the ambiguity; no
    warning or instrument was suppressed.
85. The first Redis-attempt verifier used command-line SQL quotes that PowerShell
    stripped. PostgreSQL rejected the query and the gate aborted/restored the
    dependency. SQL is now streamed on standard input; the uninterrupted rerun
    measured two attempts during five seconds and three after recovery.
86. The first optional-observer run found a disposable stopped dashboard bound
    to a removed Docker network. The verifier now removes/recreates only that
    in-memory container; observer-stop independence then passed repeatedly.
87. The first expanded clean-copy run exposed an old harness command that
    restarted the API without its shared-network sidecar. The product correctly
    remained unready. All rate-limit resets now restart the pair; the running
    isolated gate was healed by restarting its sidecar and then passed every
    remaining assertion. The corrected command is retained for the next run.
88. Restarting the API and sidecar together in one Compose `restart` command
    still raced because Docker may operate on both concurrently. That clean-copy
    rerun aborted and restored the main stack. The final helper explicitly stops
    sidecar then app, starts app then sidecar, and the next untouched fresh copy
    exercised all three resets and every Phase 2–10 gate unattended as V-1105.

89. The first broad Gitleaks filesystem scan included ignored runtime spike data
    and correctly detected a generated data-protection key. The gate now scans
    only clean copyable repository material, while CI scans committed history;
    runtime/ignored state is neither distributed source nor silently exempted.
90. The first Phase 11 header assertion required an exact value, but nginx and the
    API both emitted the defensive header and HTTP combined the duplicates. The
    security property is presence, so the test now asserts the directive exists;
    the full authorization/abuse matrix reran successfully.
91. Three early performance-harness compilations used C# features unsupported by
    Windows PowerShell's older `Add-Type` compiler and initially lacked the
    explicit HTTP assembly reference. The harness was rewritten to the supported
    language subset and then executed the real 120-request profile; no result was
    inferred from the failed harness attempts.
92. A targeted `docker compose up -d --build web --wait` rebuilt/replaced an API
    sharing a Dapr sidecar network namespace without recreating the pair. The
    known R-042 lifecycle rule restored the pair, and the untouched clean-copy
    run later completed without the stale namespace.
93. The initial Phase 11 formatting gate found existing whitespace drift in
    `InvoiceEndpoints.cs`. Repository formatting was applied mechanically and
    `dotnet format --verify-no-changes`, build and tests all passed; no analyzer or
    warning was suppressed.
94. The previously pinned nginx final image produced four vulnerable packages and
    20 critical/high findings in Docker Scout. The base was upgraded to the exact
    digest recorded in the Dockerfile; rebuilt web images then had no
    critical/high finding in both the focused and clean-copy advisory snapshots.
95. The first database-operations run reached the intended PostgreSQL schema
    denial, but PowerShell treated native stderr as terminating before the harness
    could assert it. The negative probe now captures only that command with
    non-terminating native error handling, requires a nonzero exit plus
    `permission denied`, and the complete rerun passed with zero temporary objects.
96. The first GitHub CLI winget installation remained stuck in Windows Installer
    and installed nothing. Its verified processes were inspected; winget was
    stopped, while the protected installer service could not be stopped. The
    official 2.97.0 portable archive then matched published SHA-256
    `35D7FE...50CEC`. A first portable move assumed the wrong archive directory and
    installed nothing; the exact invalid PATH entry was corrected, the archive
    layout inspected, and the checksum-verified binary now reports 2.97.0.
97. GitHub-hosted run `30917243950` passed secret scanning, backend and frontend
    gates, coverage artifacts, and all three image builds, then Docker Scout
    rejected the runner account as not entitled. ADR-019's pinned-container
    fallback now uses the immutable, non-prerelease Trivy 0.73.0 release at
    verified digest `sha256:7cced7...091045c`; critical/high findings still fail, and a non-empty
    SPDX JSON SBOM remains required for each final image. The failed run is
    retained and no hosted pass is claimed until the replacement rerun succeeds.
98. Replacement run `30920502822` passed every gate but GitHub annotated four
    action families for deprecated Node 20 runtimes that the hosted runner had
    forcibly redirected to Node 24. Current official releases and their checked-in
    `action.yml` metadata were inspected, then checkout, setup-node,
    upload-artifact, setup-buildx and build-push were repinned to full commit SHAs
    for their current Node 24 releases. The warning was not suppressed; a final
    hosted rerun remains required before R-020 closes.
99. A pre-push YAML parse command found neither ambient Python nor PyYAML in the
    bundled Python runtime, so it produced no parse evidence. The independent
    full-SHA/whitespace checks passed; the change modifies only action-reference
    scalars, and GitHub's own workflow parser plus execution remains the required
    validation. No parser success was inferred from either failed command.

The earlier Docker-blocked rows are retained as historical results. Their `-R1`
reruns above are the current evidence.
