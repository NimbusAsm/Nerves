# Nerves

> Self-hosted account center: one legal entity, one verified identity, many network accounts.
> **Not** the Elixir [Nerves](https://nerves-project.org) embedded framework — different project, same name.

An open-source account center you deploy yourself: one login page, a standards-based
OpenID Connect provider, and an admin console. Built as microservices so it can grow
from a single node to a sharded cluster.

**Status: M0 (skeleton).** Nothing is usable yet; this repository is being rebuilt from scratch.

## Layout

```text
src/
  Nerves.Core/              domain primitives (Result, Error, IDs, events)
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
