/**
 * Runs the SPA's own OIDC module from Node, against a real stack.
 *
 * ```bash
 * node tools/flow-check.ts
 * ```
 *
 * It exercises exactly the code the browser runs (`src/auth.ts`): login ticket,
 * PKCE authorize, code exchange, userinfo — only the redirect is replaced by a
 * `redirect: 'manual'` fetch that reads the `Location` header.
 */

import {
  authorizationUrl,
  exchangeCode,
  fetchUserInfo,
  loginForTicket,
  memoryStore,
  newPkce,
  newState,
} from '../src/auth.ts'

function env(name: string, fallback: string): string {
  const value = process.env[name]
  return value === undefined || value === '' ? fallback : value
}

const idpBase = env('NERVES_IDP', 'http://127.0.0.1:5101')
const username = env('NERVES_USERNAME', 'admin')
const password = env('NERVES_PASSWORD', '')
const clientId = env('NERVES_CLIENT_ID', 'nerves-admin')
const redirectUri = env('NERVES_REDIRECT_URI', 'http://127.0.0.1:5178/auth/callback')
const scopes = env('NERVES_SCOPES', 'openid profile email nerves.api offline_access')

if (password === '') {
  console.error('NERVES_PASSWORD is required')
  process.exit(2)
}

const config = {
  idpBase,
  gatewayBase: env('NERVES_GATEWAY', 'http://127.0.0.1:5100'),
  clientId,
  redirectUri,
  scopes,
}

const store = memoryStore()

console.log('Nerves SPA auth module, driven from Node')
console.log('  IdP      ' + config.idpBase)
console.log('  client   ' + config.clientId)
console.log('  redirect ' + config.redirectUri)
console.log()

const ticket = await loginForTicket(config, username, password)
console.log('  1. login api        ok (ticket ' + ticket.length + ' chars)')

const pkce = await newPkce()
const state = newState()
const url = authorizationUrl(config, { ticket, challenge: pkce.challenge, state })
console.log('  2. authorize url    ok (PKCE S256, state ' + state.length + ' chars)')

const redirect = await fetch(url, { redirect: 'manual' })
const location = redirect.headers.get('location')
if (location === null) {
  console.error('  authorize returned no Location header (status ' + redirect.status + ')')
  process.exit(1)
}

const code = new URL(location).searchParams.get('code')
const returnedState = new URL(location).searchParams.get('state')
if (code === null) {
  console.error('  authorize redirected without a code: ' + location)
  process.exit(1)
}

if (returnedState !== state) {
  console.error('  state was not echoed back')
  process.exit(1)
}

console.log('  3. authorize        ok (code ' + code.length + ' chars, state echoed)')

const tokens = await exchangeCode(config, code, pkce.verifier)
console.log(
  '  4. token endpoint   ok (access ' +
    tokens.accessToken.length +
    ' chars, refresh ' +
    (tokens.refreshToken?.length ?? 0) +
    ' chars)',
)

const profile = await fetchUserInfo(config, tokens.accessToken)
console.log('  5. userinfo         ok (sub ' + profile.sub + ', name ' + (profile.name ?? '-') + ')')
console.log()
console.log('SPA auth flow ok')
