# Resilience Matrix

Status: **VERIFIED by V-1102, V-1103, V-1104 and V-1105**

| Boundary | Failure types | Operation timeout | Retry owner | Maximum/cadence | Fallback or circuit | Terminal/operator state |
|---|---|---:|---|---|---|---|
| PostgreSQL | Refused connection, timeout, transient connection/query failure | 5s connect; 10s command; 2s cancellation | Npgsql/EF execution strategy for declared transient failures | 3 retries, at most 2s delay | Readiness 503; liveness remains 200 | Synchronous safe error; durable rows recover when database returns |
| API Dapr sidecar | Refused connection, timeout, non-success publish | 3s per HTTP call | Outbox publisher | One call per lease; durable retry cadence capped at 30s | API readiness 503; no in-memory retry loop | Outbox row retains attempts, last error, age and next attempt |
| Automation Dapr sidecar | Refused connection, RPC timeout/error | 3s readiness; SDK cancellation per command | Workflow-command dispatcher | 10 durable attempts; backoff capped at 30s | Automation readiness 503 | Failed command timestamp/error and operator snapshot |
| Redis Pub/Sub | At-least-once redelivery, unavailable stream | Dapr component policy | Dapr inbound resiliency | 5 deliveries total, 2s constant delay | Dead-letter topic | Unique inbox absorbs duplicates; dead letter persists |
| Workflow activity | Transient activity/provider failure | Activity cancellation and provider timeout | Dapr Workflow only | 3 attempts per automatic/manual cycle; 2s then 4s | Durable workflow result | Notification delivery/attempt rows retain terminal outcome |
| Mailpit API/SMTP | Refused connection, timeout, SMTP 4xx/5xx | 3s API; 5s SMTP cancellation | Workflow activity only | No HTTP/SMTP retry; Workflow owns 3-attempt cycle | Readiness degraded, not unhealthy; no fake success | Pending/Failed/Cancelled delivery plus attempts and stable Message-ID |
| Browser GET | Network error or 5xx | Browser/fetch cancellation | Vue Query | 1 automatic retry | User-visible retry | Error state; previous validated cache may remain |
| Browser mutation | Timeout/unknown outcome | Browser/fetch cancellation | No automatic retry | 0 | Stable request/resource/idempotency identity supports reconciliation | Honest error/conflict state |
| Optional OTLP/Aspire | Viewer/export connection loss | Exporter-owned bounded queue/export | OpenTelemetry exporter | No business-operation retry | Drop telemetry; JSON stdout remains | Application readiness and business path remain independent |
| nginx/web to API | API stopped/unready or DNS replacement | Browser request bound/cancelled by navigation | User/manual retry only | No server retry layer | Static web remains available; API route returns 502 | No mutation success claim without validated API response |
| Placement/scheduler | Control-plane interruption | 1s TCP readiness probe; 3s workflow command | Dapr Workflow/control plane; command dispatcher only for command submission | 10 durable command submissions; no activity retry layer | Automation readiness 503; existing durable state remains in PostgreSQL/scheduler volume | Pending/failed command snapshot and restart recovery runbook |

No `TBD` value may replace these bounds without a superseding ADR and executable
evidence. Durable outbox retry is intentionally recovery polling, not multiple
attempts inside one request; each lease owns exactly one publish call.
