# Vue SPA sample

Public client: authorization code + **PKCE**, no client secret, tokens in
**memory**, and the management API called through `@nerves/sdk`.

```text
SPA ──POST /idp/api/v1/auth/login──▶ (proxied) IdP   → login ticket
 │──location.assign(/idp/connect/authorize?…)────────▶ IdP
 │◀─302 http://127.0.0.1:5178/auth/callback?code&state
 │──POST /idp/connect/token (code + verifier)────────▶ IdP  → tokens (memory)
 └──GET /gateway/api/v1/accounts (Bearer)──▶ @nerves/sdk ─▶ gateway
```

## Run it

```bash
# 1) the SDK the SPA imports must be built once
pnpm --dir ../../sdk/typescript install
pnpm --dir ../../sdk/typescript build

cd samples/vue-spa
npm install
cp .env.example .env.local
SPA_PORT=5178 npm run dev
# open http://127.0.0.1:5178
```

Register the redirect URI first: add
`http://127.0.0.1:5178/auth/callback` to `OpenIddictSeed:AdminRedirectUris` in
`src/services/Nerves.Idp/appsettings.Development.json` (development seed) and
restart the IdP, or register a dedicated client for the SPA.

## Why the dev proxy

The browser must reach the IdP **and** the gateway. Rather than widening
`Cors:AdminOrigins` for a sample, `vite.config.ts` proxies `/idp` and
`/gateway` server-side, so every request the page makes is same-origin. In
production you pick one:

| Deployment | Approach |
| --- | --- |
| Static SPA + IdP on another origin | add the SPA origin to `Cors:AdminOrigins`; keep PKCE |
| Anything sensitive | use the BFF pattern (`samples/node-bff`) and keep tokens server-side |

## Token policy

| Item | Where it lives | Why |
| --- | --- | --- |
| access / ID / refresh token | module variable in `src/auth.ts` | gone on refresh, invisible to XSS persistence |
| PKCE verifier + `state` | `sessionStorage` | must survive the redirect to the IdP |
| username / password | form fields only | never stored |

`localStorage` is deliberately unused: it would keep a stolen token alive
across tabs and reloads.

## Verify without a browser

`src/auth.ts` is DOM-free, so the same code runs from Node:

```bash
NERVES_PASSWORD=… NERVES_REDIRECT_URI=http://127.0.0.1:5178/auth/callback \
  node tools/flow-check.ts
```

Expected:

```text
Nerves SPA auth module, driven from Node
  IdP      http://127.0.0.1:5101
  client   nerves-admin
  redirect http://127.0.0.1:5178/auth/callback

  1. login api        ok (ticket 43 chars)
  2. authorize url    ok (PKCE S256, state 22 chars)
  3. authorize        ok (code 43 chars, state echoed)
  4. token endpoint   ok (access 1231 chars, refresh 3176 chars)
  5. userinfo         ok (sub 01a1…, name Nerves Administrator)

SPA auth flow ok
```

## What a production SPA must add

- refresh before `expiresAt` (or a silent-renew iframe), and rotate on use;
- a strict CSP — the whole token model assumes no script injection;
- `state` is validated here; keep it that way, do not drop it for brevity;
- do not put the access token in a URL, a `<title>`, or an analytics event.
