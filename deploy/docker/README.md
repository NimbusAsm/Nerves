# Local stack

docker compose -f deploy/docker/docker-compose.yml up -d --build starts
PostgreSQL and all eight services. Every service listens on :8080 inside the
network; the host mapping is:

| Service | Host port | Purpose |
| --- | --- | --- |
| gateway | 5100 | public entry point (REST) |
| idp | 5101 | OIDC endpoints and the login API |
| identity | 5102 / 5202 | accounts and credentials |
| session | 5103 / 5203 | sessions |
| authorization | 5104 / 5204 | roles and decisions |
| audit | 5105 / 5205 | audit trail |
| directory | 5106 / 5206 | organizations |
| compliance | 5107 | Compliance.CN |

The internal gRPC ports (5202-5206) must stay HTTP/2; the REST ports are HTTP/1.1.

## What the stack does on first start

* Database__MigrateOnStart=true applies every EF migration.
* The identity service seeds admin / ChangeMe!123 when DevSeed__Enabled=true.
* The authorization service waits for that account and grants it nerves:admin.

The issuer is pinned to http://127.0.0.1:5101/ while services discover the
signing keys through the container network, so tokens minted inside the stack
remain valid when the browser reaches the IdP through the published port.

## Production

Use deploy/helm/nerves. Migrations run from a dedicated job there, the signing
key comes from OpenIddict__SigningCertificatePath, and dev seeding is off.


## Full stack with the admin console

```bash
docker compose --profile admin up -d --build
```

The admin image is built from apps/admin with VITE_API_BASE_URL and VITE_IDP_URL
pointing at the local gateway and IdP, and is served on http://127.0.0.1:5300/.
The profile keeps it out of the default `up` so backend-only runs stay fast.
