# Nerves integration samples

Three runnable applications, one per integration shape. Every sample is
standalone: its own `.env.example`, its own locked dependency set, and a README
that gets you from clone to "tokens issued" in well under 30 minutes.

| Sample | Shape | Client type | Tokens live | Uses |
| --- | --- | --- | --- | --- |
| [`dotnet-console`](dotnet-console/) | headless client / CI job | public + PKCE | process memory | `Nerves.Client` (.NET SDK) |
| [`node-bff`](node-bff/) | backend for frontend | public + PKCE, server-side callback | Node process; browser gets an opaque cookie | plain `fetch` |
| [`vue-spa`](vue-spa/) | single-page app | public + PKCE | browser memory only | `@nerves/sdk` |

## The flow they all walk

```text
POST {idp}/api/v1/auth/login        {username, password}  → {loginTicket}
GET  {idp}/connect/authorize        client_id, redirect_uri, scope, state,
                                    code_challenge(S256), login_ticket
                                    → 302 {redirect_uri}?code=&state=
POST {idp}/connect/token            grant_type=authorization_code, code,
                                    redirect_uri, client_id, code_verifier
                                    → access_token, id_token, refresh_token
GET  {idp}/connect/userinfo         Bearer access_token → sub, name, email, leid
GET  {gateway}/api/v1/accounts      Bearer access_token → management API
```

Two things surprise people coming from other IdPs:

1. **The IdP renders no pages.** `/connect/authorize` needs a `login_ticket`
   from the login API, which the *client* collects with its own UI. That is why
   a console app can complete an interactive flow.
2. **PKCE is mandatory** (`RequireProofKeyForCodeExchange()`), for confidential
   clients too. Dropping `code_verifier` fails the token call.

## Prerequisites

| Item | Value |
| --- | --- |
| Stack | `docker compose -f deploy/docker/docker-compose.yml up -d postgres nats`, then the services (see [quickstart](/guide/quickstart)) |
| IdP | `http://127.0.0.1:5101` |
| Gateway | `http://127.0.0.1:5100` |
| Client | `nerves-admin` (public, PKCE) is seeded in Development |
| Redirect URIs | seeded: `http://127.0.0.1:5300/oidc-callback.html` — add your sample's URI before running it |

## Registering a redirect URI locally

OIDC requires an exact match. In development the clients come from
`OpenIddictSeed` in `src/services/Nerves.Idp/appsettings.Development.json`:

```json
"OpenIddictSeed": {
  "Enabled": true,
  "AdminClientId": "nerves-admin",
  "AdminRedirectUris": [
    "http://127.0.0.1:5300/oidc-callback.html",
    "http://127.0.0.1:4311/auth/callback",
    "http://127.0.0.1:5178/auth/callback"
  ]
}
```

Restart the IdP and the seeder reconciles the registered URIs. In production,
register the client through your deployment process instead of the seeder
(`ProductionGuards` refuses to seed outside Development).

## Conventions used by all three samples

- no secret is ever committed; `.env.example` holds placeholders only;
- errors are printed, never swallowed — a sample that hides a 401 teaches nothing;
- every sample states what a production version still needs (refresh, storage,
  CSRF, cookie flags), so the code is not mistaken for a reference implementation.
