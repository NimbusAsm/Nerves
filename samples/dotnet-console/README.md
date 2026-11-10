# .NET console sample

Headless walk of the Nerves login chain:

```text
POST /api/v1/auth/login  →  GET /connect/authorize (PKCE)  →  POST /connect/token
                                                                ↓
                            GET /connect/userinfo  ←──  tokens
                                                                ↓
                            GET /api/v1/accounts (gateway, via Nerves.Client SDK)
```

The point is the shape: `/connect/authorize` only needs a `login_ticket` from the
login API, so a console tool, a CLI, or a CI job can complete the code + PKCE
flow without a browser.

## Run it (2 minutes once a stack is up)

```bash
cd samples/dotnet-console
cp .env.example .env      # then set NERVES_PASSWORD
set -a && . ./.env && set +a
dotnet run
```

Expected output:

```text
Nerves .NET sample
  IdP      http://127.0.0.1:5101
  gateway  http://127.0.0.1:5100
  client   nerves-admin
  redirect http://127.0.0.1:5300/oidc-callback.html

  1. login api        ok (single-use ticket, 90 s TTL)
  2. oidc authorize   ok (code issued, PKCE S256)
  3. token endpoint   ok (access + refresh issued, expires in 3600s)
  4. userinfo         ok (sub 019…, name …, leid …)
  5. management api   ok (5 account(s) in the first page, cursor 01a1…)
```

## What each step proves

| Step | Endpoint | What to look at |
| --- | --- | --- |
| 1 | `POST /api/v1/auth/login` | `loginTicket` is single use and lives 90 seconds |
| 2 | `GET /connect/authorize` | the code arrives in the `Location` header; `state` is echoed back |
| 3 | `POST /connect/token` | `expires_in`, `refresh_token`, `id_token` |
| 4 | `GET /connect/userinfo` | `sub`, `name`, `email`, optional `leid` |
| 5 | `GET /api/v1/accounts` | the SDK signs the request with the access token |

## Guardrails this sample demonstrates

- the login ticket is redeemed **once**: replaying the authorize request fails;
- PKCE is mandatory server-side, so dropping `code_verifier` fails the token call;
- the access token is bearer-only — put it in memory, never in a URL or a log;
- the SDK retries 429/5xx with backoff and throws `NervesApiException` otherwise.

## Troubleshooting

| Symptom | Cause |
| --- | --- |
| `401 login failed` | wrong password, or the account is locked out after five failures |
| `authorize failed: 400 invalid_redirect_uri` | `NERVES_REDIRECT_URI` is not registered for the client |
| `authorize redirected without a code` | client is not allowed the code flow, or PKCE was rejected |
| `management api failed: 403` | the account has no `nerves:admin` permission |
