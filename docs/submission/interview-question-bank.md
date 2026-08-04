# Interview Question Bank

These prompts are based on the implemented ADRs, observed failures, alternatives
and the Software Engineer III Full Stack role themes: complex system design,
scalability/performance/future requirements, security/operations/resilience/cost,
APIs/data/query optimization, automated delivery and technical leadership.

## Architecture and trade-offs

1. **Why not microservices?** The product has one synchronous transactional
   domain, so a modular monolith avoids distributed transactions and deployment
   overhead. Automation is separate only because durable timing and delivery have
   distinct failure ownership. Evidence: ADR-001 and the clean Compose path.
2. **Why Dapr, and when would you remove it?** It owns real durable workflow and
   at-least-once concerns, adopted only after timer/restart/early-event spikes.
   Remove it for Hangfire if pinned behavior or operational cost stops being
   defensible. Evidence: ADR-003, V-0101-R1/V-0102-R1 and Phase 8 gates.
3. **Where is consistency strong versus eventual?** Invoice mutation, audit and
   outbox are one PostgreSQL transaction. Broker publication, workflow command
   dispatch and notification are eventual, durable and reconciled. A browser
   success never promises email delivery.
4. **Why PostgreSQL for business and workflow state but Redis for Pub/Sub?**
   Financial truth and recovery need relational constraints/transactions;
   Redis is only Dapr transport. The separation prevents cache/stream availability
   from becoming business truth.
5. **How would this evolve for higher scale?** Measure first; preserve tenant
   predicates and bounded APIs, use keyset pagination for deep scans, isolate
   hot read models, deploy shared edge limiting and managed data/workflow planes.
   Do not infer a target capacity from the local 120-request diagnostic.

## Correctness, concurrency and idempotency

6. **What happens if issue executes twice?** The stable command ID and persisted
   command/audit/outbox identities return the existing result for the same input;
   conflicting reuse returns 409. The allocator remains monotonic and unique.
7. **How are concurrent payments prevented from overpaying?** Each writer loads a
   versioned aggregate, domain logic checks remaining balance, PostgreSQL optimistic
   concurrency lets one commit, and the loser receives a conflict for reread.
8. **Why not promise gapless invoice numbers?** Rollback, failure and distributed
   allocation make that operationally expensive. The implemented promise is
   unique monotonic per business, stated in ADR-007 and proven concurrently.
9. **What happens after a crash between database commit and publish?** The outbox
   row remains. A leased publisher retries; duplicate transport is absorbed by a
   unique inbox/stable workflow command. Reconciliation repairs business/workflow
   drift.
10. **Who owns retries?** One layer per boundary: outbox publisher, Dapr inbound,
    workflow-command dispatcher, Dapr Workflow activity, or Vue read query.
    Mutations do not auto-retry. Recite the exact resilience matrix bounds.

## Security and tenancy

11. **Why cookies instead of JWT in browser storage?** Same-origin Identity cookies
    avoid exposing bearer credentials to JavaScript; HttpOnly/Strict cookies plus
    antiforgery protect the browser boundary. OIDC is the external-identity fallback.
12. **How do you prevent IDOR?** Authentication alone is insufficient. Every
    resource path rereads active membership, authorizes the minimum role and
    filters by both tenant and resource ID; foreign/missing resources return the
    same 404. The matrix tests every actor category.
13. **What remains for a real deployment?** TLS termination, managed secrets and
    encrypted/rotated data-protection keys, edge/DDoS controls, distributed rate
    limiting, authenticated operator plane, backup policy and monitoring ownership.
14. **Why is the demo password acceptable?** It is explicitly local, supplied by
    `.env.example`, required rather than silently defaulted, and seeding refuses
    non-Development. It is not a production secret or credential pattern.

## API, data and frontend

15. **Why runtime validation when TypeScript already compiles?** Network data is
    untrusted at runtime. Generated Zod rejects malformed success payloads and the
    UI renders a recoverable contract error. Generated drift is a CI failure.
16. **How do collection endpoints remain safe?** Page/page-size validation, maximum
    100, deterministic ordering, server-side filters/sorts and demonstrated
    indexes. Dashboard lists are fixed at 5/10; audit is paginated.
17. **How is remote search race-safe?** Vue debounces input, Vue Query keys include
    search/paging state, obsolete fetches receive an AbortSignal, and the server
    bounds/escapes search before indexed tenant-filtered execution.
18. **How do frontend errors avoid a white screen?** Runtime schemas fail at the
    API boundary, Problem Details normalize known failures, root/feature error
    containment provides recovery, and important screens render pending, empty,
    error, forbidden/conflict and success states.
19. **What accessibility evidence exists?** Native labelled controls, landmarks,
    focusable validation summaries, live status, visible focus, axe/component
    tests and the recorded human keyboard script. This is not claimed as formal
    WCAG certification.

## Operations and delivery

20. **What makes readiness truthful?** It probes required PostgreSQL/Dapr/control
    plane dependencies with bounds. Liveness only says the process responds.
    Mailpit is degraded because durable work can continue. Failure/recovery tests
    assert the actual status codes.
21. **How would an operator diagnose one failed notification?** Start with the
    operations snapshot, then correlate trace/workflow/invoice/delivery/message
    IDs through structured logs, workflow command, delivery and attempt rows.
22. **Why is retained overdue history not an outbox alert?** It has no business
    consumer in this topology. The status/metric separates retained history from
    publishable backlog; adding a cosmetic consumer would increase cost without
    reducing risk.
23. **How are migrations deployed?** A one-shot migrator with schema privilege
    precedes apps; applications use a separate DML-only identity in the rehearsal.
    SQL is generated/reviewed, model drift fails, earlier snapshots upgrade and
    repeat execution preserves inventory.
24. **What does CI prove and not prove?** It proves the published commit can restore,
    build, format, test, generate, scan, produce SBOMs/images and run Compose on a
    GitHub-hosted runner. It does not prove production capacity, uptime, compliance
    or future freedom from advisories.

## Failure stories and leadership discussion

25. **Describe a failure that improved the design.** Examples: malformed live UTC
    formatting was safely rejected by Zod; API/sidecar network-namespace recreation
    required paired ordering; an immediate delivery assertion raced durable state;
    the initial image scan found nginx vulnerabilities; Phase 12 found unbounded
    Phase 2/audit collections and ambiguous outbox metrics. In every case evidence
    failed first, the cause was corrected, and the entire relevant gate reran.
26. **What would you challenge in a code review?** Any new endpoint without tenant/
    role/CSRF and collection bounds, nested retry layers, client-calculated totals,
    mutation auto-retry, runtime-contract bypass, readiness that checks only process,
    migration on replica startup, secret fallback, or prose unsupported by a test.
27. **How would you explain the architecture to stakeholders?** Lead with product
    risk: financial correctness and tenant isolation are synchronous; notifications
    can be delayed but must not be lost; optional telemetry cannot block billing.
    Then show cost/alternatives and the bounded limitations, not technology names.
28. **What would you do next with another week?** Choose from measured need:
    external OIDC/managed secrets, deployment-specific TLS/backup/alerts, membership
    administration, provider-backed delivery, deeper accessibility review, or
    production load characterization. Do not add all of them for appearance.
