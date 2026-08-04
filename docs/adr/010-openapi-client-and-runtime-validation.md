# ADR-010: OpenAPI client and runtime validation generation

- Status: Accepted
- Date: 2026-07-30
- Requirements: REQ-016
- Risks: R-009, R-010
- Verification: V-0106

## Context

Compile-time TypeScript types cannot reject a malformed runtime response. Decimal,
date-time, UUID, enum, nullability, pagination and Problem Details shapes are
high-risk drift points.

## Decision

Generate the OpenAPI document at .NET build time with
Microsoft.AspNetCore.OpenApi 10.0.10 and ApiDescription.Server 10.0.10. Generate
TypeScript SDK/types/Zod schemas using exact-pinned `@hey-api/openapi-ts` 0.99.0.
Use TypeScript 6.0.3, because 7.0.2 was incompatible. Represent money at the HTTP
boundary as a two-decimal string and dates as RFC 3339 strings. Validate responses
at trust boundaries.

Keep `strict` and `noUncheckedIndexedAccess`; do not enable
`exactOptionalPropertyTypes` until generated fetch runtime supports it. Pin
Microsoft.OpenApi 2.7.5 and js-yaml 4.3.0 for published advisories.

## Primary evidence

The official OpenAPI and Hey API sources and advisories are linked in the
primary-source record. Server build regenerates the document. The generator,
Vue/TypeScript build, seven malformed/valid Zod tests, NuGet scans and npm audit
pass.

## Alternatives considered

- Kiota plus handwritten Zod: stable fallback but creates an explicit schema drift
  surface.
- Handwritten fetch/types: rejected due to silent drift.
- Generated TypeScript types without runtime schemas: rejected because network
  data is untrusted.

## Consequences and failure modes

The selected generator calls itself initial-development software, so exact pins
and fixture tests are mandatory. Upgrades may change output or compiler
compatibility. An RFC-invalid all-ones UUID fixture was correctly rejected and
replaced with an RFC-compatible value; strict JSON number handling was added
after numeric strings were incorrectly accepted.

## Reversal or fallback path

If a pinned upgrade fails generation, compile, audit or fixture tests, retain the
last passing pin or move to Kiota plus explicit Zod schemas and a drift test.

## Verification

Run the API Release build, `npm run generate`, `npm run build`,
`npm test -- --run`, `npm audit --audit-level=high`, and both NuGet vulnerability
scans. Recorded as V-0106.
