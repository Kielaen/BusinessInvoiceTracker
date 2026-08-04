# Phase 12 Hostile Review

Date: 2026-08-04. Scope: current application source, generated contract,
Compose topology, verification scripts, reviewer documentation and operational
claims. This is an evidence-focused engineering review, not a penetration test
or a production-capacity certification.

## Review lenses and inspected surfaces

| Lens | Inspected surfaces | Result |
|---|---|---|
| Authentication, authorization and tenant isolation | Cookie/antiforgery pipeline, endpoint policies, resource lookups, Phase 2/5/6/7/9/11 abuse matrices | No bypass found after retained and clean-copy reruns |
| Replay, concurrency and restart | Customer/invoice command identities, versions, outbox/inbox/workflow/delivery ownership, shutdown gate | One shutdown harness ordering defect found and corrected; durable work recovered within the declared attempt bound |
| Bounded data access | Business, membership, customer, invoice, audit and dashboard queries | Three unbounded reviewer-facing collections found and bounded |
| Contract and frontend integrity | OpenAPI output, generated SDK/Zod, Vue query adapters, async/error/accessibility states | Numeric OpenAPI union exposed a production-build error; normalized at the view boundary and covered by a behavior test |
| Health and operations truth | Live/ready endpoints, durable inventories, optional observer, runbook language | Retained domain-event history was ambiguous with publishable backlog; metrics/status now distinguish them and SQL comparison passes |
| Delivery and clean clone | Root README, Compose profiles, scripts, fresh volumes, scans, migration and role gates | One stale Phase 7 verifier assumed the old audit response shape; corrected to inspect the bounded page and rerun without weakening the assertion |
| Documentation claims | README, ADR index, threat model, architecture, risks, requirements, runbooks and limitations | Stale phase labels and evidence states reconciled; non-claims remain explicit |

## Findings and dispositions

### F-1301 — Retained events looked like a transport backlog

The operator snapshot counted deliberately retained `InvoiceBecameOverdue`
domain-event history together with publishable outbox work. That could cause an
operator to diagnose a transport failure that did not exist. The automation
monitor now excludes retained history from publishable count/age and exposes a
separate `invoice.domain.events.retained` gauge and status field. The Phase 10
gate compares that field with a direct bounded SQL count. No cosmetic consumer
was added. R-053 records the distinction and future journal fallback.

### F-1302 — Reviewer-facing collections were not uniformly bounded

Business and owner-membership lists, plus invoice audit history, could return an
unbounded response. They now use validated page/page-size inputs, a maximum page
size of 100 and deterministic ordering. The audit view has accessible paging;
OpenAPI, TypeScript and Zod artifacts were regenerated. The signed-in user's
small session-membership context has deterministic ordering and a hard cap of
100; no public mutation path can grow it without the owner boundary.

### F-1303 — Generated numeric types broke the Vue production build

The regenerated contract represents JSON numbers as `string | number`, while a
view computed value assumed a number. The first production build failed. The
view now normalizes the pagination value with `Number(...)`; the complete build
and generated-drift checks pass. The failure remains in the evidence ledger.

### F-1304 — New contract behavior fell below the configured coverage floor

The first complete regression run passed all tests but reported 24.9% function
coverage against the unchanged 25% floor. A behavior test was added for the
bounded audit request and validated response. The rerun passed 21 .NET tests,
27 Vue tests and all existing coverage floors. No threshold was lowered and no
warning or failure was suppressed.

### F-1305 — Concurrent Compose stop could spend an extra retry attempt

The first Phase 11 rerun recovered every durable row and receipt but reached five
attempts, above the declared maximum four. Inspection disproved the initial
unique-ID suspicion: the fixture already generated unique identities. Stopping
the app and its shared-network Dapr sidecar concurrently allowed the sidecar to
exit first while the publisher remained alive. The gate now stops the API first,
then its sidecar, matching the documented paired lifecycle rule. The focused and
complete reruns recovered 12/12 rows and 12 receipts with a maximum four
attempts. R-060 preserves the operational constraint and fallback.

### F-1306 — Phase 7 clean-copy assertion used the pre-pagination shape

The first Phase 12 clean-copy run reached Phase 7 after passing locked builds,
tests and earlier gates, then failed because the script read `audit.action`
instead of `audit.items.action`. The assertion now checks the generated bounded
page while still requiring both issue and payment audit actions. The failed run
is retained. The untouched rerun from copied source and fresh volumes passed the
complete Phase 2 and 4–11 suite as V-1306-R1.

## Residual limitations

No authentication bypass, cross-tenant disclosure, fake readiness result, dead
configuration presented as active, secret, or untriaged critical/high package
finding was found by the executed gates. The bounded non-claims and operational
assumptions that remain are maintained in
[`docs/submission/known-limitations.md`](../submission/known-limitations.md).
