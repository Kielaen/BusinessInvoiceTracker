# Architecture Evidence

The current end-to-end architecture, correctness boundaries, retry ownership,
deployment shape, alternatives and non-claims are in
[`submission-architecture-and-tradeoffs.md`](submission-architecture-and-tradeoffs.md).
The walking-skeleton topology below is retained as the Phase 1 evidence snapshot;
it is not the complete submission architecture.

## Executable Phase 1 topology

```text
Browser :8080
  -> nginx (Vue assets and /api same-origin proxy)
  -> Invoice.Api
       -> PostgreSQL (probe + outbox in one SaveChanges transaction)
       -> local Dapr sidecar -> Redis Stream
                              -> automation Dapr sidecar
                              -> Invoice.Automation
                                   -> PostgreSQL (unique inbox + receipt transaction)
```

The app and its sidecar share a network namespace, so each app reaches its own
Dapr HTTP API on `127.0.0.1:3500`. nginx resolves `api` through Docker DNS every
five seconds so API container replacement does not retain a stale address.

The API owns browser authentication, antiforgery and business persistence. The
worker has no browser route. Its Phase 1 host port exists only for health and
duplicate-delivery verification and should not be exposed by a production
deployment.

## Delivery semantics

- PostgreSQL is the business source of truth.
- The outbox publisher retries only the API-to-Dapr boundary, with a maximum
  30-second delay between attempts.
- Dapr/Redis delivery is at least once.
- The worker inserts the stable message ID into a unique inbox in the same
  transaction as its receipt effect.
- Multiple delivery attempts can publish or invoke more than once; the business
  effect is once for this walking-skeleton event.
- A producer activity flows through Dapr's CloudEvent trace context to the worker.
  Message ID remains the correlation key after traces expire.

This is a single-API/single-worker Phase 1 topology. Concurrent outbox claim
ownership and product crash-window tests remain explicitly deferred to Phase 8.
