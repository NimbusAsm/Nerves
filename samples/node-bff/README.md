# Node / TypeScript BFF sample

A **backend for frontend**: the browser holds only an opaque `HttpOnly` session
cookie; access and refresh tokens stay in the Node process.

```text
browser ──POST /login──▶ BFF ──POST /api/v1/auth/login──▶ IdP   (login ticket)
   │                      │
   │◀──302 /connect/authorize?...login_ticket&code_challenge──┘
   ▼
 IdP (cookie + PKCE) ──302 /auth/callback?code──▶ BFF ──POST /connect/token──▶ tokens
                                                        │
 browser ──GET /api/accounts──▶ BFF ──Bearer──▶ gateway /api/v1/accounts
```

## Run it

```bash
cd samples/node-bff
npm install
cp .env.example .env && set -a && . ./.env && set +a
npm start
# open http://127.0.0.1:4311
```

Register the redirect URI first. In local development the clients are seeded by
`OpenIddictSeed` in `src/services/Nerves.Idp/appsettings.Development.json`; add
`http://127.0.0.1:4311/auth/callback` to `OpenIddictSeed:AdminRedirectUris` (or
give the sample its own client) and restart the IdP.

Expected console output:

```text
Nerves BFF sample listening on http://127.0.0.1:4311
  IdP      http://127.0.0.1:5101
  gateway  http://127.0.0.1:5100
  client   nerves-admin
  redirect http://127.0.0.1:4311/auth/callback
```

## Routes

| Route | Purpose |
| --- | --- |
| `GET /` | login form, or the signed-in summary |
| `POST /login` | login API → authorize redirect (state + PKCE stored server-side) |
| `GET /auth/callback` | verify state, exchange code, create the server-side session |
| `GET /me` | JSON view of the session (handy for `curl`) |
| `GET /api/accounts` | management API call with the session's access token |
| `GET /logout` | drop the session and the cookie |

## Why this shape

- **tokens never enter JavaScript** — no `localStorage`, no token in a URL;
- **state and PKCE verifier live server-side**, so a stolen redirect URL is useless;
- **one cookie, opaque** — rotating the session does not rotate the tokens;
- the access token is used **server-to-server**, so the IdP needs no CORS entry
  for this client.

## What a production BFF must add

| Gap | Why it matters |
| --- | --- |
| Token refresh before `expiresAt` | the sample does not refresh; a long-lived tab will 401 |
| Session store in Redis/Postgres | `Map` dies with the process, so every restart signs everyone out |
| `POST /logout` + `/connect/logout` | clearing the cookie does not revoke the IdP session |
| CSRF token on `POST /login` | SameSite=Lax helps, but do not rely on it alone |
| `secure: true` cookies | required behind HTTPS |
| Rate limiting on `/login` | the IdP throttles, but the BFF should too |
