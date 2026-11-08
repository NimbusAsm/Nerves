import { setAccessToken } from './token'
import type { NervesUser } from './types'

const PENDING_KEY = 'nerves.admin.oidc.pending.v1'

export interface OidcConfig {
  idpUrl: string
  clientId: string
  redirectUri: string
  scope: string
}

export const oidcConfig: OidcConfig = {
  idpUrl: (import.meta.env.VITE_IDP_URL ?? 'http://127.0.0.1:5101').replace(/\/$/, ''),
  clientId: import.meta.env.VITE_OIDC_CLIENT_ID ?? 'nerves-admin',
  redirectUri: window.location.origin + '/oidc-callback.html',
  scope: 'openid profile email nerves.api',
}

interface PendingAuthorization {
  verifier: string
  state: string
  redirectTo: string
}

export interface OidcSession {
  token: string
  user: NervesUser
  redirectTo: string
}

function base64Url(bytes: Uint8Array): string {
  let binary = ''
  for (const byte of bytes) binary += String.fromCharCode(byte)
  return btoa(binary).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '')
}

function randomString(byteLength = 32): string {
  return base64Url(crypto.getRandomValues(new Uint8Array(byteLength)))
}

async function challengeFor(verifier: string): Promise<string> {
  const digest = await crypto.subtle.digest('SHA-256', new TextEncoder().encode(verifier))
  return base64Url(new Uint8Array(digest))
}

function decodeJwtPayload(token: string): Record<string, unknown> {
  const payload = token.split('.')[1] ?? ''
  const padded = payload + '='.repeat((4 - (payload.length % 4)) % 4)
  const json = atob(padded.replace(/-/g, '+').replace(/_/g, '/'))
  return JSON.parse(json) as Record<string, unknown>
}

function stringClaim(claims: Record<string, unknown>, name: string): string {
  const value = claims[name]
  return typeof value === 'string' ? value : ''
}

function userFromClaims(idClaims: Record<string, unknown>, accessClaims: Record<string, unknown>, fallback: string): NervesUser {
  const permissions = stringClaim(accessClaims, 'permissions').split(' ').filter(Boolean)

  // The first-party console is fully trusted; nerves:admin unlocks every page.
  const effective = permissions.includes('nerves:admin') ? ['*'] : permissions

  const email = stringClaim(idClaims, 'email')
  const name = stringClaim(idClaims, 'name')
  const fallbackName = name || email || 'user'

  return {
    id: stringClaim(idClaims, 'sub') || fallback,
    username: email ? email.split('@')[0] ?? email : fallbackName,
    displayName: fallbackName,
    email,
    roles: [],
    permissions: effective,
  }
}

/**
 * Step one: exchange credentials for a single-use login ticket and send the
 * browser to the IdP authorization endpoint using PKCE.
 */
export async function beginLogin(username: string, password: string, redirectTo: string): Promise<void> {
  const response = await fetch(oidcConfig.idpUrl + '/api/v1/auth/login', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ username, password }),
  })

  if (!response.ok) {
    const problem = (await response.json().catch(() => null)) as { message?: string; detail?: string } | null
    throw new Error(problem?.message ?? problem?.detail ?? '登录失败')
  }

  const body = (await response.json()) as { loginTicket: string }

  const verifier = randomString()
  const state = randomString(16)
  const challenge = await challengeFor(verifier)
  const pending: PendingAuthorization = { verifier, state, redirectTo }
  sessionStorage.setItem(PENDING_KEY, JSON.stringify(pending))

  const params = new URLSearchParams({
    client_id: oidcConfig.clientId,
    response_type: 'code',
    scope: oidcConfig.scope,
    redirect_uri: oidcConfig.redirectUri,
    state,
    code_challenge: challenge,
    code_challenge_method: 'S256',
    login_ticket: body.loginTicket,
  })

  window.location.assign(oidcConfig.idpUrl + '/connect/authorize?' + params.toString())
}

/** Step two: the IdP redirected back; exchange the code for tokens. */
export async function completeLogin(search: string): Promise<OidcSession> {
  const params = new URLSearchParams(search)
  const error = params.get('error')
  if (error) {
    sessionStorage.removeItem(PENDING_KEY)
    throw new Error(params.get('error_description') ?? '登录未完成')
  }

  const code = params.get('code')
  const state = params.get('state')
  const raw = sessionStorage.getItem(PENDING_KEY)
  sessionStorage.removeItem(PENDING_KEY)

  if (!code || !state || !raw) throw new Error('登录状态已失效，请重试')

  const pending = JSON.parse(raw) as PendingAuthorization
  if (pending.state !== state) throw new Error('登录状态校验失败，请重试')

  const form = new URLSearchParams({
    grant_type: 'authorization_code',
    code,
    redirect_uri: oidcConfig.redirectUri,
    client_id: oidcConfig.clientId,
    code_verifier: pending.verifier,
  })

  const response = await fetch(oidcConfig.idpUrl + '/connect/token', {
    method: 'POST',
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    body: form.toString(),
  })

  if (!response.ok) throw new Error('令牌交换失败')

  const tokens = (await response.json()) as { access_token: string; id_token: string }
  setAccessToken(tokens.access_token)

  return {
    token: tokens.access_token,
    user: userFromClaims(decodeJwtPayload(tokens.id_token), decodeJwtPayload(tokens.access_token), pending.redirectTo),
    redirectTo: pending.redirectTo,
  }
}
