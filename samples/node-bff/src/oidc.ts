/**
 * Framework-free OIDC helpers shared by the BFF sample.
 *
 * Only the pieces a backend needs: PKCE, the login API, the code exchange and
 * userinfo. Everything talks to the Nerves IdP directly (server-to-server), so
 * no CORS configuration is involved.
 */

export interface BffConfig {
  /** IdP origin, e.g. http://127.0.0.1:5101 */
  idpBase: string
  /** Gateway origin, e.g. http://127.0.0.1:5100 */
  gatewayBase: string
  clientId: string
  /** Must be registered on the client verbatim. */
  redirectUri: string
  scopes: string
}

export interface PkcePair {
  verifier: string
  challenge: string
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

export interface Session {
  id: string
  tokens: Tokens
  profile: Profile
}

function base64Url(bytes: Uint8Array): string {
  return Buffer.from(bytes).toString('base64url')
}

/** 32 random bytes, SHA-256 challenged (S256). */
export async function newPkce(): Promise<PkcePair> {
  const verifier = base64Url(crypto.getRandomValues(new Uint8Array(32)))
  const digest = await crypto.subtle.digest('SHA-256', new TextEncoder().encode(verifier))
  return { verifier, challenge: base64Url(new Uint8Array(digest)) }
}

export function newState(): string {
  return base64Url(crypto.getRandomValues(new Uint8Array(16)))
}

async function postJson(url: string, body: unknown): Promise<unknown> {
  const response = await fetch(url, {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify(body),
  })

  const text = await response.text()
  if (!response.ok) {
    throw new Error(`${url} failed: ${response.status} ${text}`)
  }

  return JSON.parse(text)
}

/** Step 1: credentials in, single-use login ticket out. */
export async function loginForTicket(
  config: BffConfig,
  username: string,
  password: string,
): Promise<string> {
  const payload = await postJson(`${config.idpBase}/api/v1/auth/login`, { username, password })

  if (typeof payload !== 'object' || payload === null) {
    throw new Error('login API returned no object')
  }

  const record = payload as Record<string, unknown>
  if (record.mfaRequired === true) {
    throw new Error('this account requires a second factor; the sample does not implement MFA')
  }

  const ticket = record.loginTicket
  if (typeof ticket !== 'string' || ticket.length === 0) {
    throw new Error('login API returned no loginTicket')
  }

  return ticket
}

/** Step 2: build the authorization URL the browser is sent to. */
export function authorizationUrl(
  config: BffConfig,
  input: { ticket: string; challenge: string; state: string },
): string {
  const url = new URL('/connect/authorize', config.idpBase)
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

/** Step 3: code + verifier in, tokens out. */
export async function exchangeCode(
  config: BffConfig,
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

  const response = await fetch(new URL('/connect/token', config.idpBase), {
    method: 'POST',
    headers: { 'content-type': 'application/x-www-form-urlencoded' },
    body: form.toString(),
  })

  const text = await response.text()
  if (!response.ok) {
    throw new Error(`token exchange failed: ${response.status} ${text}`)
  }

  const payload = JSON.parse(text) as Record<string, unknown>
  const accessToken = payload.access_token
  if (typeof accessToken !== 'string') {
    throw new Error('token response carried no access_token')
  }

  const expiresIn = typeof payload.expires_in === 'number' ? payload.expires_in : 3600
  return {
    accessToken,
    idToken: typeof payload.id_token === 'string' ? payload.id_token : null,
    refreshToken: typeof payload.refresh_token === 'string' ? payload.refresh_token : null,
    expiresAt: Date.now() + expiresIn * 1000,
  }
}

/** Step 4: profile claims for the signed-in account. */
export async function userInfo(config: BffConfig, accessToken: string): Promise<Profile> {
  const response = await fetch(new URL('/connect/userinfo', config.idpBase), {
    headers: { authorization: `Bearer ${accessToken}` },
  })

  const text = await response.text()
  if (!response.ok) {
    throw new Error(`userinfo failed: ${response.status} ${text}`)
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
