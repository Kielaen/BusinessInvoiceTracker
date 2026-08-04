# Phase 12 Working Plan

Status: in progress on 2026-08-04. Phase 12 is the only active phase.

## Objective and scope

Make every submission claim inspectable, rerun the reviewer path from clean
source, close documentation contradictions, and prepare an evidence-based demo
and interview defence. This phase may correct defects found by hostile review,
but it does not add optional product scope or claim a real production deployment.

## Submission-gate matrix

| Gate condition | Required Phase 12 proof | Failure condition |
|---|---|---|
| Brief and governing requirements are complete | Final requirements traceability links every row to runnable or inspectable evidence | Any requirement remains `Unstarted`, `In progress`, or supported only by prose |
| Public source is safe to review | Clean material and full Git history secret scans; public repository and hosted CI links | Secret, sensitive path, or untriaged critical/high finding |
| Clean clone builds, runs, and tests | Exact README commands plus isolated clean-copy gate | Any undocumented prerequisite, missing artifact, or stale command |
| Security boundary holds | Anonymous, role, CSRF, resource and cross-tenant matrix | Authentication bypass, wrong-role mutation, or resource disclosure |
| Correctness survives duplicates/concurrency/restart | Existing PostgreSQL, lifecycle, outbox, workflow, notification and shutdown gates | Lost work, duplicate business effect, overpayment, stale overwrite, or unsafe replay |
| Health and diagnostics are truthful | Dependency failure/recovery, readiness and diagnostic inventory evidence | Ready while a required dependency is unusable, fake aggregate, or unactionable failure |
| Frontend and API contract agree | Generated drift, malformed-response, component, accessibility and live E2E gates | Handwritten contract drift, non-functional control, hidden failure, or inaccessible required journey |
| Documentation matches implementation | Hostile review plus final README, architecture, limitations, demo and interview documents | Contradictory claim, dead configuration presented as active, or test claim without evidence |
| Major choices are defensible | ADR index and architecture/trade-off summary link decisions, alternatives, consequences and fallbacks | Material architecture assumption without evidence or fallback |

## Hostile-review lenses

The review searches specifically for authorization bypasses, cross-tenant query
leaks, unsafe retries, replay and concurrency races, restart gaps, fake health,
dead configuration, unbounded queries, inactive UI controls, contract drift,
hardcoded secrets, clean-clone dependencies, and documentation/test claims that
do not match executable evidence. The original SE3 role material and prior
evaluation reports make these recurring reviewer concerns; they are supporting
context, not proof that this repository passes.

## Execution order

1. Inventory current source, docs, risks, public state and submission claims.
2. Perform the hostile code/configuration/documentation review and correct any
   material defect with focused evidence.
3. Produce the final architecture/trade-off summary, known limitations, demo
   script and ADR/failure-based interview question bank.
4. Reconcile requirements, risks, threat model, runbooks and root README.
5. Rerun every README command and the complete isolated clean-copy, E2E,
   failure, scan and migration suite; record commands and failures truthfully.
6. Commit and push the evidence, then require a successful GitHub-hosted run at
   the final commit before closing the phase.

## Decisions and fallbacks

- No production SLO, cloud deployment, backup schedule, WAF, distributed rate
  limiter, zero-downtime migration, or penetration-test claim will be inferred
  from local assessment evidence.
- A material concern found by review blocks the gate until fixed and rerun, or
  until a simpler bounded limitation is explicit and defensible under the plan.
- Optional infrastructure is removed or kept outside the default stack if it
  weakens clean-clone reliability.
- Historical failed evidence remains in the ledger; final reruns append results
  and never rewrite earlier failures as successes.

## Exit conditions

Every Phase 12 and constitution submission-gate row must pass at the same final
commit, every README command must have a recorded successful rerun, all current
documentation must agree with code, and no material concern may remain without
an explicit bounded limitation. Phase 12 remains active until those conditions
and the final hosted CI run pass.
