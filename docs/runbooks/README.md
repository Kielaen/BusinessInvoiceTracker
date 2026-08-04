# Phase 1 Local Runbook

Phase 10 dependency failure, backlog, notification correlation and optional
telemetry procedures are in `phase-10-operations.md`.

## Start and verify

```powershell
docker compose --env-file .env.example up --build -d
.\scripts\verify-compose.ps1
```

Inspect state with:

```powershell
docker compose --env-file .env.example ps
docker compose --env-file .env.example logs --no-color api api-dapr
docker compose --env-file .env.example logs --no-color automation automation-dapr
```

Correlate a request by the `messageId` returned by the verifier. The API
`Published outbox message` and worker `Processed walking-skeleton message` entries
must contain that value and share a trace ID for the normal Dapr delivery.

## Dependency diagnosis

- API not ready: inspect PostgreSQL health, migration exit code, then API logs.
- Worker not ready: inspect PostgreSQL and `/v1.0/healthz` in its Dapr sidecar.
- Probe remains pending: inspect outbox publish attempts, Dapr subscription logs,
  Redis pending entries, then worker exceptions. Redis redelivery is configured
  to scan every 2 seconds after a 10-second processing timeout.
- Browser receives an unexpected 404 after API replacement: verify the running
  web image contains the Docker DNS resolver configuration, then reproduce with
  `scripts/verify-compose.ps1`. The stale-address failure and fix were exercised
  in V-0206.

## Optional telemetry

```powershell
.\scripts\verify-compose.ps1 -Observability
```

The gate queries the dashboard telemetry API and requires logs, traces, and
metrics for both .NET services. The dashboard at
`http://127.0.0.1:18888` is unauthenticated, local-only and in-memory.

## Clean shutdown

```powershell
docker compose --env-file .env.example down --volumes --remove-orphans
```

This removes local PostgreSQL, Redis and data-protection test volumes. It does not
remove source files or built images.
