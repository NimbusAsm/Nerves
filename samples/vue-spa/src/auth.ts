/**
 * OIDC core for the SPA sample — deliberately DOM-free so the same code can be
 * exercised from Node (`node tools/flow-check.ts`).
 *
 * Token policy: access / ID / refresh tokens live in a module-level variable
 * (memory only). Nothing sensitive touches `localStorage`; only the transient
 * PKCE verifier and `state` are parked in `sessionStorage`, because the browser
 * navigates away to the IdP and back.
 */

export interface OidcConfig {
  /** Same-origin prefix that the dev proxy forwards to the IdP. */
  idpBase: string
  /** Same-origin prefix that the dev proxy forwards to the gateway. */
  gatewayBase: string
  clientId: string
  redirectUri: string
  scopes: string
}

export interface Tokens {
  accessToken: string
  idToken: string | null
  refreshToken: string | null
  expiresAt: number
}

export interface Profile {
  sub: string
  name?: string
  email?: string
  leid?: string
}

/** Minimal storage abstraction so tests can swap in memory. */
export interface KeyValueStore {
  get(key: string): string | null
  set(key: string, value: string): void
  remove(key: string): void
}

const VERIFIER_KEY = 'nerves.pkce.verifier'
const STATE_KEY = 'nerves.pkce.state'

export function memoryStore(): KeyValueStore {
  const map = new Map<string, string>()
  return {
    get: (key) => map.get(key) ?? null,
    set: (key, value) => {
      map.set(key, value)
    },
    remove: (key) => {
      map.delete(key)
    },
  }
}

/** `sessionStorage` wrapper; survives the redirect, dies with the tab. */
export function webStorageStore(): KeyValueStore {
  return {
    get: (key) => window.sessionStorage.getItem(key),
    set: (key, value) => window.sessionStorage.setItem(key, value),
    remove: (key) => window.sessionStorage.removeItem(key),
  }
}

let tokens: Tokens | null = null

export function currentTokens(): Tokens | null {
  return tokens
}

export function clearTokens(): void {
  tokens = null
}

function base64Url(bytes: Uint8Array): string {
  return btoa(String.fromCharCode(...bytes))
    .replaceAll('+', '-')
    .replaceAll('/', '_')
    .replaceAll('=', '')
}

export async function newPkce(): Promise<{ verifier: string; challenge: string }> {
  const verifier = base64Url(crypto.getRandomValues(new Uint8Array(32)))
  const digest = await crypto.subtle.digest('SHA-256', new TextEncoder().encode(verifier))
  return { verifier, challenge: base64Url(new Uint8Array(digest)) }
}

export function newState(): string {
  return base64Url(crypto.getRandomValues(new Uint8Array(16)))
}

/** Step 1: credentials in, single-use login ticket out. */
export async function loginForTicket(
  config: OidcConfig,
  username: string,
  password: string,
): Promise<string> {
  const response = await fetch(config.idpBase + '/api/v1/auth/login', {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify({ username, password }),
  })

  const text = await response.text()
  if (!response.ok) {
    throw new Error('login failed: ' + response.status + ' ' + text)
  }

  const payload = JSON.parse(text) as Record<string, unknown>
  if (payload.mfaRequired === true) {
    throw new Error('this account requires a second factor; the sample does not implement MFA')
  }

  const ticket = payload.loginTicket
  if (typeof ticket !== 'string') {
    throw new Error('login API returned no loginTicket')
  }

  return ticket
}

/** Step 2: build the authorization URL to navigate to. */
export function authorizationUrl(
  config: OidcConfig,
  input: { ticket: string; challenge: string; state: string },
): string {
  const url = new URL(config.idpBase + '/connect/authorize')
  url.searchParams.set('client_id', config.clientId)
  url.searchParams.set('response_type', 'code')
  url.searchParams.set('scope', config.scopes)
  url.searchParams.set('redirect_uri', config.redirectUri)
  url.searchParams.set('state', input.state)
  url.searchParams.set('code_challenge', input.challenge)
  url.searchParams.set('code_challenge_method', 'S256')
  url.searchParams.set('login_ticket', input.ticket)
  return url.toString()
}

/** Step 3: code + verifier in, tokens in memory out. */
export async function exchangeCode(
  config: OidcConfig,
  code: string,
  verifier: string,
): Promise<Tokens> {
  const form = new URLSearchParams({
    grant_type: 'authorization_code',
    code,
    redirect_uri: config.redirectUri,
    client_id: config.clientId,
    code_verifier: verifier,
  })

  const response = await fetch(config.idpBase + '/connect/token', {
    method: 'POST',
    headers: { 'content-type': 'application/x-www-form-urlencoded' },
    body: form.toString(),
  })

  const text = await response.text()
  if (!response.ok) {
    throw new Error('token exchange failed: ' + response.status + ' ' + text)
  }

  const payload = JSON.parse(text) as Record<string, unknown>
  const accessToken = payload.access_token
  if (typeof accessToken !== 'string') {
    throw new Error('token response carried no access_token')
  }

  const expiresIn = typeof payload.expires_in === 'number' ? payload.expires_in : 3600
  tokens = {
    accessToken,
    idToken: typeof payload.id_token === 'string' ? payload.id_token : null,
    refreshToken: typeof payload.refresh_token === 'string' ? payload.refresh_token : null,
    expiresAt: Date.now() + expiresIn * 1000,
  }
  return tokens
}

/** Step 4: profile claims. */
export async function fetchUserInfo(config: OidcConfig, accessToken: string): Promise<Profile> {
  const response = await fetch(config.idpBase + '/connect/userinfo', {
    headers: { authorization: 'Bearer ' + accessToken },
  })

  const text = await response.text()
  if (!response.ok) {
    throw new Error('userinfo failed: ' + response.status + ' ' + text)
  }

  const payload = JSON.parse(text) as Record<string, unknown>
  const sub = payload.sub
  if (typeof sub !== 'string') {
    throw new Error('userinfo carried no sub')
  }

  return {
    sub,
    name: typeof payload.name === 'string' ? payload.name : undefined,
    email: typeof payload.email === 'string' ? payload.email : undefined,
    leid: typeof payload.leid === 'string' ? payload.leid : undefined,
  }
}

/** Starts the redirect flow; returns the URL to navigate to. */
export async function beginLogin(
  config: OidcConfig,
  store: KeyValueStore,
  username: string,
  password: string,
): Promise<string> {
  const ticket = await loginForTicket(config, username, password)
  const pkce = await newPkce()
  const state = newState()
  store.set(VERIFIER_KEY, pkce.verifier)
  store.set(STATE_KEY, state)
  return authorizationUrl(config, { ticket, challenge: pkce.challenge, state })
}

/** Finishes the redirect flow after the IdP sends the browser back. */
export async function completeLogin(
  config: OidcConfig,
  store: KeyValueStore,
  params: { code: string | null; state: string | null },
): Promise<Tokens> {
  const verifier = store.get(VERIFIER_KEY)
  const expectedState = store.get(STATE_KEY)
  store.remove(VERIFIER_KEY)
  store.remove(STATE_KEY)

  if (params.code === null || params.state === null) {
    throw new Error('the callback is missing code or state')
  }

  if (verifier === null || expectedState === null) {
    throw new Error('no pending PKCE transaction; start the sign-in again')
  }

  if (params.state !== expectedState) {
    throw new Error('state mismatch — possible CSRF, refusing the callback')
  }

  const tokens = await exchangeCode(config, params.code, verifier)
  return tokens
}

export function configFromEnv(): OidcConfig {
  const env = import.meta.env
  return {
    idpBase: env.VITE_NERVES_IDP ?? '/idp',
    gatewayBase: env.VITE_NERVES_GATEWAY ?? '/gateway',
    clientId: env.VITE_NERVES_CLIENT_ID ?? 'nerves-admin',
    redirectUri: env.VITE_NERVES_REDIRECT_URI ?? window.location.origin + '/auth/callback',
    scopes: env.VITE_NERVES_SCOPES ?? 'openid profile email nerves.api offline_access',
  }
}
