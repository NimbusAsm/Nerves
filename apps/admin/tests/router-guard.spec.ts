import { createMemoryHistory } from 'vue-router'
import { beforeEach, describe, expect, it } from 'vitest'

import { createAppRouter } from '@/router'
import { safeRedirect } from '@/lib/url'
import { authContext, guardRouter } from './helpers'

describe('router guard', () => {
  let auth = authContext()

  beforeEach(() => {
    auth = authContext()
  })

  it('sends anonymous visitors to the login page and remembers the target', async () => {
    auth = authContext({ isAuthenticated: false, hasPermission: () => false })
    const router = guardRouter(() => auth)

    await router.push('/accounts')

    expect(router.currentRoute.value.name).toBe('login')
    expect(router.currentRoute.value.query.redirect).toBe('/accounts')
  })

  it('keeps anonymous visitors on the login page', async () => {
    auth = authContext({ isAuthenticated: false })
    const router = guardRouter(() => auth)

    await router.push('/login')

    expect(router.currentRoute.value.name).toBe('login')
    expect(router.currentRoute.value.query.redirect).toBeUndefined()
  })

  it('redirects signed-in operators away from the login page', async () => {
    const router = guardRouter(() => auth)

    await router.push('/login')

    expect(router.currentRoute.value.name).toBe('dashboard')
  })

  it('blocks routes the session has no permission for', async () => {
    const router = guardRouter(() => authContext({ hasPermission: (code) => code === 'stats:read' }))

    await router.push('/audit')

    expect(router.currentRoute.value.name).toBe('forbidden')
  })

  it('allows routes covered by the permission code', async () => {
    const router = guardRouter(() => authContext())

    await router.push('/audit')

    expect(router.currentRoute.value.name).toBe('audit')
  })

  it('is wired into the real application router', async () => {
    const router = createAppRouter(
      () => authContext({ isAuthenticated: false }),
      createMemoryHistory(),
    )

    await router.push('/accounts')

    expect(router.currentRoute.value.name).toBe('login')
    expect(router.currentRoute.value.query.redirect).toBe('/accounts')
  })
})

describe('safeRedirect', () => {
  it('accepts in-app paths', () => {
    expect(safeRedirect('/accounts?q=zhang')).toBe('/accounts?q=zhang')
    expect(safeRedirect('/accounts')).toBe('/accounts')
  })

  it('rejects external and protocol-relative targets', () => {
    expect(safeRedirect('https://evil.example')).toBe('/')
    expect(safeRedirect('//evil.example')).toBe('/')
    expect(safeRedirect('/\\evil.example')).toBe('/')
    expect(safeRedirect(undefined)).toBe('/')
    expect(safeRedirect(42)).toBe('/')
  })
})
