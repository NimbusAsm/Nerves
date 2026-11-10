import { serve } from '@hono/node-server'
import { Hono } from 'hono'
import { deleteCookie, getCookie, setCookie } from 'hono/cookie'
import { randomUUID } from 'node:crypto'
import {
  authorizationUrl,
  exchangeCode,
  loginForTicket,
  newPkce,
  newState,
  userInfo,
  type BffConfig,
  type Session,
} from './oidc.ts'

/**
 * Backend-for-frontend sample.
 *
 * The browser only ever holds an opaque HttpOnly session cookie. Access and
 * refresh tokens stay in this process; the page talks to the BFF, the BFF talks
 * to Nerves. That is the shape most first-party web apps want, because it keeps
 * tokens out of JavaScript entirely.
 */

function env(name: string, fallback: string): string {
  const value = process.env[name]
  return value === undefined || value === '' ? fallback : value
}

const config: BffConfig = {
  idpBase: env('NERVES_IDP', 'http://127.0.0.1:5101'),
  gatewayBase: env('NERVES_GATEWAY', 'http://127.0.0.1:5100'),
  clientId: env('NERVES_CLIENT_ID', 'nerves-admin'),
  redirectUri: env('NERVES_REDIRECT_URI', 'http://127.0.0.1:4311/auth/callback'),
  scopes: env('NERVES_SCOPES', 'openid profile email nerves.api offline_access'),
}

const port = Number(env('BFF_PORT', '4311'))
const sessionCookie = 'nerves_bff'
const sessionTtlMs = 60 * 60 * 1000
const pendingTtlMs = 10 * 60 * 1000

interface Pending {
  verifier: string
  createdAt: number
}

const sessions = new Map<string, Session>()
const pending = new Map<string, Pending>()

function prune(): void {
  const cutoff = Date.now() - pendingTtlMs
  for (const [state, entry] of pending) {
    if (entry.createdAt < cutoff) {
      pending.delete(state)
    }
  }
}

function escapeHtml(value: string): string {
  return value
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;')
}

function page(title: string, body: string): string {
  return [
    '<!doctype html>',
    '<html lang="en">',
    '<head>',
    '<meta charset="utf-8">',
    '<meta name="viewport" content="width=device-width, initial-scale=1">',
    '<title>' + escapeHtml(title) + '</title>',
    '<style>',
    'body{font:15px/1.5 ui-sans-serif,system-ui,sans-serif;margin:0;background:#0b0f14;color:#e6edf3}',
    'main{max-width:44rem;margin:3rem auto;padding:0 1.25rem}',
    'h1{font-size:1.35rem;margin:0 0 .35rem}p.muted{color:#8b98a5;margin:.2rem 0 1.5rem}',
    'form{display:grid;gap:.75rem;max-width:22rem}',
    'input{background:#111823;border:1px solid #26313d;color:inherit;padding:.55rem .65rem;border-radius:.4rem}',
    'button{background:#2f81f7;border:0;color:#fff;padding:.6rem .9rem;border-radius:.4rem;cursor:pointer}',
    'table{border-collapse:collapse;width:100%;font-size:.9rem}',
    'th,td{text-align:left;padding:.45rem .6rem;border-bottom:1px solid #1c2530}',
    'code{background:#111823;padding:.1rem .3rem;border-radius:.25rem}',
    'a{color:#58a6ff}',
    '</style>',
    '</head>',
    '<body><main>',
    body,
    '</main></body></html>',
  ].join('')
}

const loginForm = page(
  'Sign in',
  [
    '<h1>Nerves BFF sample</h1>',
    '<p class="muted">Credentials go to the BFF, which calls the login API. ',
    'The browser never sees a token.</p>',
    '<form method="post" action="/login">',
    '<input name="username" placeholder="username" autocomplete="username" required>',
    '<input name="password" type="password" placeholder="password" autocomplete="current-password" required>',
    '<button type="submit">Sign in</button>',
    '</form>',
  ].join(''),
)

const app = new Hono()

app.get('/', (c) => {
  const id = getCookie(c, sessionCookie)
  const session = id === undefined ? undefined : sessions.get(id)

  if (session === undefined) {
    return c.html(loginForm)
  }

  const profile = session.profile
  const body = [
    '<h1>Signed in</h1>',
    '<p class="muted">Session stays on the server; the cookie is HttpOnly.</p>',
    '<p><strong>' + escapeHtml(profile.name ?? profile.sub) + '</strong>',
    profile.email === undefined ? '' : ' &lt;' + escapeHtml(profile.email) + '&gt;',
    '</p>',
    '<p>sub <code>' + escapeHtml(profile.sub) + '</code>',
    profile.leid === undefined ? '' : ' · leid <code>' + escapeHtml(profile.leid) + '</code>',
    '</p>',
    '<p><a href="/api/accounts">GET /api/accounts</a> · <a href="/logout">Sign out</a></p>',
  ].join('')

  return c.html(page('Signed in', body))
})

app.post('/login', async (c) => {
  prune()

  const body = await c.req.parseBody()
  const username = typeof body.username === 'string' ? body.username : ''
  const password = typeof body.password === 'string' ? body.password : ''

  if (username === '' || password === '') {
    return c.text('username and password are required', 400)
  }

  try {
    const ticket = await loginForTicket(config, username, password)
    const pkce = await newPkce()
    const state = newState()
    pending.set(state, { verifier: pkce.verifier, createdAt: Date.now() })

    return c.redirect(
      authorizationUrl(config, { ticket, challenge: pkce.challenge, state }),
      302,
    )
  } catch (error) {
    const message = error instanceof Error ? error.message : 'login failed'
    return c.html(page('Sign in failed', '<h1>Sign in failed</h1><p class="muted">' + escapeHtml(message) + '</p><p><a href="/">Try again</a></p>'), 401)
  }
})

app.get('/auth/callback', async (c) => {
  const code = c.req.query('code')
  const state = c.req.query('state')
  const error = c.req.query('error')

  if (error !== undefined) {
    return c.html(page('Sign in failed', '<h1>Sign in failed</h1><p class="muted">' + escapeHtml(error) + '</p>'), 400)
  }

  if (code === undefined || state === undefined) {
    return c.text('code and state are required', 400)
  }

  const entry = pending.get(state)
  if (entry === undefined) {
    return c.text('unknown or expired state', 400)
  }

  pending.delete(state)

  try {
    const tokens = await exchangeCode(config, code, entry.verifier)
    const profile = await userInfo(config, tokens.accessToken)
    const id = randomUUID()
    sessions.set(id, { id, tokens, profile })

    setCookie(c, sessionCookie, id, {
      httpOnly: true,
      sameSite: 'Lax',
      path: '/',
      secure: false,
      maxAge: Math.floor(sessionTtlMs / 1000),
    })

    return c.redirect('/', 302)
  } catch (exchangeError) {
    const message = exchangeError instanceof Error ? exchangeError.message : 'token exchange failed'
    return c.html(page('Sign in failed', '<h1>Sign in failed</h1><p class="muted">' + escapeHtml(message) + '</p>'), 401)
  }
})

app.get('/me', (c) => {
  const id = getCookie(c, sessionCookie)
  const session = id === undefined ? undefined : sessions.get(id)

  if (session === undefined) {
    return c.json({ authenticated: false }, 401)
  }

  return c.json({
    authenticated: true,
    profile: session.profile,
    expiresAt: new Date(session.tokens.expiresAt).toISOString(),
  })
})

app.get('/api/accounts', async (c) => {
  const id = getCookie(c, sessionCookie)
  const session = id === undefined ? undefined : sessions.get(id)

  if (session === undefined) {
    return c.json({ error: 'not authenticated' }, 401)
  }

  const target = new URL('/api/v1/accounts', config.gatewayBase)
  target.searchParams.set('limit', '5')

  const response = await fetch(target, {
    headers: { authorization: 'Bearer ' + session.tokens.accessToken },
  })

  const text = await response.text()
  return c.body(text, response.status as 200, {
    'content-type': response.headers.get('content-type') ?? 'application/json',
  })
})

app.get('/logout', (c) => {
  const id = getCookie(c, sessionCookie)
  if (id !== undefined) {
    sessions.delete(id)
  }

  deleteCookie(c, sessionCookie, { path: '/' })
  return c.redirect('/', 302)
})

serve({ fetch: app.fetch, port, hostname: '127.0.0.1' }, (info) => {
  console.log('Nerves BFF sample listening on http://127.0.0.1:' + info.port)
  console.log('  IdP      ' + config.idpBase)
  console.log('  gateway  ' + config.gatewayBase)
  console.log('  client   ' + config.clientId)
  console.log('  redirect ' + config.redirectUri)
})
