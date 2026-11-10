# Changelog

All notable changes to Nerves are documented here. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project follows
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2026-11-11

First production-ready release of the unified account platform.

### Added

- Legal entity + network account model with invariants I1-I7 enforced in the domain.
- OIDC provider (authorization code + PKCE, refresh tokens with rotation and reuse
  detection), JSON login API with one-time tickets, TOTP and passkey MFA, recovery
  codes, account switching, back-channel logout.
- Session service with configurable idle/absolute lifetimes and expiry sweeping.
- RBAC authorization with groups, permission caching, external PDP support and
  SCIM 2.0 users/groups endpoints.
- Directory service (organizations, invitations), audit service with hash-chained
  streams, NDJSON archives and CSV/JSON export.
- Notification service with email templates, delivery log and retrying worker.
- LDAP directory sync that provisions and updates accounts idempotently, with a status endpoint.
- Self-service password reset with single-use hashed tokens.
- Pluggable real-name verification providers (manual/http) with asynchronous attempts.
- Runtime OIDC application management API and a live admin catalogue.
- Read-only console aggregation API and the nerves CLI (migrate/seed/doctor/shard reconcile).
- Retention-policy enforcement and audit outboxes in session/authorization/directory/compliance.
- Notification SMS channel and optional identity-event consumption.
- Supply-chain gates (Trivy, SBOM, OpenAPI diff, k6 smoke), scheduled backup CronJob and admin Ingress.
- Compliance.CN service: consent records, retention policies, data subject export
  and erasure.
- Identity domain-event outbox with incremental `/api/v1/events` reads and durable
  delivery to the audit stream.
- Routing-index shard reconciliation tooling for shard-count changes.
- Read-only console aggregation API (`Nerves.ConsoleApi`) that fans out to the
  services in parallel and degrades to warnings when one of them is unavailable.
- `nerves` operations CLI (dotnet tool): database migrations, development seed,
  health doctor and routing-index shard reconciliation.
- Prometheus metrics, OpenTelemetry export, health and readiness probes.
- Admin console (Vue 3) container image with OIDC PKCE login.
- Multi-language SDKs: .NET, TypeScript, Python.
- Release runbook, restore-drill script and OpenAPI archiving.
- Helm chart (9 services, migration jobs, PVCs), Docker Compose stack, and
  operations runbooks (backup/restore, key rotation, upgrade, security checklist).

### Security

- JWT Bearer validation with issuer/audience pinning and permission claims.
- DataProtection key ring persistence; production refuses development seeds.
- Login lockout persisted per identifier, rate limits on auth endpoints.
- Forwarded-header support behind TLS-terminating proxies.
- Dependency vulnerability gate in CI and Dependabot updates.

### Fixed

- Admin console callback now reads the authorization response from the hash route.
- Disabling a legal entity or account revokes its sessions immediately.

[1.0.0]: https://github.com/NimbusAsm/Nerves/releases/tag/v1.0.0
