# Nerves

> Self-hosted account center: one legal entity, one verified identity, many network accounts.
> **Not** the Elixir [Nerves](https://nerves-project.org) embedded framework — different project, same name.

An open-source account center you deploy yourself: a login API every product can embed,
a standards-based OpenID Connect provider, and an API-only server. Built as microservices so it can grow
from a single node to a sharded cluster.

**Status: end-to-end flow works.** Browser login (OIDC authorization code + PKCE) issues
signed tokens carrying the caller's legal entity and permission snapshot; sessions are
created on login and revoked on logout; every identity and login event lands in a
hash-chained audit trail; the Vue admin console, the Helm chart and the .NET,
TypeScript and Python SDKs are in place.

Not yet implemented: organizations and SCIM (the directory service is a shell),
MFA, userinfo and back-channel logout, integration tests, and image builds.
See docs/guide/status.md for the authoritative capability list.

## Layout

```text
src/
  Nerves.Core/              domain primitives (Result, Reason, IDs, events)
  Nerves.ServiceDefaults/   health, problem details, OpenTelemetry
  Nerves.Contracts.Grpc/    protobuf contracts + generated types
  services/                 gateway, idp, identity, session, authorization, directory
proto/                      the single source of truth for internal contracts
tests/                      unit tests (integration tests arrive with M0 week 2)
deploy/                     compose (dev) and Helm (production)
docs/                       documentation site (later)
```

## Build

Requires the .NET 10 SDK (pinned in `global.json`).

```bash
dotnet build
dotnet test
```

## Design

Architecture decisions are recorded as D01–D19 in `DECISIONS.md` (planning workspace);
this repository implements them.

## License

Apache-2.0. Contributions require a DCO sign-off (`git commit -s`).
