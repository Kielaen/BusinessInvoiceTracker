# Known Limitations

Status: Phase 12 submission boundaries, reviewed 2026-08-04. These limitations
are deliberate non-claims, not hidden future work.

| Boundary | What is demonstrated | What is not claimed / production responsibility |
|---|---|---|
| Hosting and scale | Complete Docker Compose topology, two API replicas for session/concurrency tests, and a 120-request local diagnostic profile | Cloud deployment, horizontal capacity, autoscaling, multi-region behavior, a production SLO, or DDoS protection |
| Transport security | Non-development HSTS, secure `__Host-` cookies, same-origin policy, CSP and tested authorization/CSRF boundaries | TLS termination in the local HTTP demo, WAF, external identity federation, penetration testing, or formal compliance |
| Secrets and keys | No detected committed secret; fail-closed demo password; persisted shared local data-protection keys | Managed secret store integration, key encryption/rotation, certificate lifecycle, or production key escrow |
| Database operations | Reviewed idempotent migrations, earlier-snapshot upgrades, one-shot migrator, local backup/restore inventory and separate-role rehearsal | A production backup schedule, point-in-time recovery, measured production lock duration, online/zero-downtime schema evolution, or managed-database HA |
| Availability | Bounded dependency calls/retries, truthful readiness, graceful-stop recovery and durable restart tests | A formal availability target, disaster recovery, host/volume-loss recovery, or chaos testing outside the local topology |
| Observability | JSON stdout, trace propagation, bounded metrics, diagnostic status and optional local Aspire viewer | A hosted telemetry backend, alert routing/on-call ownership, retention policy, production dashboards, or an authenticated deployment management plane |
| Rate limiting | ASP.NET Core login limiter and lockout behavior tested locally | Distributed rate limiting across replicas; that belongs at a shared edge or gateway |
| Money and tax | Decimal, currency, rounding and server-authoritative totals under the documented ZAR demonstration model | Legal, accounting, VAT, foreign-exchange, credit-note, or jurisdictional correctness |
| Delivery | Deterministic simulated overdue email through Mailpit with durable attempts/retries/reconciliation | A real email provider, deliverability, bounce handling, recipient verification, templates/localization, or legal notification guarantees |
| Event history | `InvoiceBecameOverdue` is retained as inspectable domain-event history and reported separately from publishable backlog | A consumer is not added without a business need; a larger product could move retained history to a dedicated event journal |
| Workflow platform | Dapr 1.17.7/.NET SDK 1.17.9 timer, event, restart, duplicate and retry behavior is pinned and tested | Cross-version upgrade compatibility beyond the pinned set or a managed Dapr control plane |
| Product scope | Secure tenant-scoped customers, invoice drafts, issue/payment/void, discovery, dashboard, audit and overdue simulation | Membership administration UI, customer self-service, PDF generation, real payment processing, credit notes, recurring invoices, attachments, or localization |
| Accessibility | Automated axe/component checks and the recorded human keyboard journey pass for required flows | A formal WCAG conformance audit across browser/assistive-technology combinations |
| Security scanning | Time-scoped NuGet/npm, committed-history secret and final-image critical/high scans plus SBOMs | A permanent guarantee against future advisories, SAST/DAST coverage, or third-party security certification |

The authoritative evidence is the
[verification ledger](../evidence/verification.md), and deployment-specific
responsibilities are also captured in the
[threat model](../security/threat-model.md).
