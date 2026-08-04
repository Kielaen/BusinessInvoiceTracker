# ADR-012: Observability baseline and optional viewer

- Status: Accepted
- Date: 2026-07-30
- Requirements: REQ-017
- Risks: R-011
- Verification: V-0107

## Context

Failures must be diagnosable, but a local dashboard must not become a default
availability dependency or inflate the assessed architecture.

## Decision

Always emit structured JSON to stdout. Instrument API and worker logs, traces,
ASP.NET/HTTP metrics and runtime metrics using exact OpenTelemetry .NET 1.17.0
packages. Export nothing unless `OTEL_EXPORTER_OTLP_ENDPOINT` is set. Provide a
digest-pinned Aspire dashboard 13.4.2 only under the `observability` profile and
activate exporters through `compose.observability.yaml`. Dapr also emits JSON and
uses OTLP only in that override.

## Primary evidence

Official .NET, Aspire and Dapr sources are linked in the primary-source record.
Both applications compile with no warnings and no vulnerable NuGet packages when
the viewer is absent. The first attempted unified exporter API did not compile;
per-signal exporters are the verified 1.17.0 API.
The optional-profile gate queries the dashboard telemetry API and requires logs,
traces, and metrics from both .NET services.

## Alternatives considered

- Always-on Aspire dashboard: rejected because local in-memory UI is not a runtime
  dependency.
- Prometheus/Grafana/Tempo/Loki stack: rejected as excess Phase 0 operations.
- Plain unstructured console: insufficient for correlation and machine diagnosis.

## Consequences and failure modes

Default startup has no exporter retry noise or viewer dependency. The optional
viewer is local-only, unauthenticated, in-memory and must never be described as
production retention. If OTLP is unreachable in the optional profile, business
requests must continue while bounded exporter queues drop telemetry.

## Reversal or fallback path

Remove the viewer and keep JSON stdout plus instrumentation if the profile fails
or consumes disproportionate resources. A hosted backend can later receive the
same OTLP signals without application redesign.

## Verification

Compile/default local auth evidence passes. Default Compose starts without the
viewer. The product optional profile reports logs, traces, and metrics for both
.NET services. A producer activity and Dapr CloudEvent propagate one W3C trace
from outbox publish into worker processing; stable message IDs provide durable
correlation. See V-0107A, V-0107B-R1, V-0204, and V-0206.
