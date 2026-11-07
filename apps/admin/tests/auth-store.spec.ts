import { beforeEach, describe, expect, it } from 'vitest'

import { ApiError } from '@/api'
import { setApi } from '@/api'
import { useAuthStore } from '@/stores/auth'
import { freshPinia, testApi } from './helpers'

describe('auth store', () => {
  beforeEach(() => {
    localStorage.clear()
    setApi(testApi())
    freshPinia()
  })

  it('signs in, persists the session and exposes permissions', async () => {
    const auth = useAuthStore()
    expect(auth.isAuthenticated).toBe(false)

    await auth.login('admin', 'nerves2026')

    expect(auth.isAuthenticated).toBe(true)
    expect(auth.user?.username).toBe('admin')
    expect(auth.token).toMatch(/^mock\.admin\./)
    expect(auth.can('identity:account:write')).toBe(true)
    expect(auth.can('does:not:exist')).toBe(false)
    expect(localStorage.getItem('nerves.admin.session.v1')).toContain('mock.admin.')

    // A brand-new store reads the persisted session.
    freshPinia()
    expect(useAuthStore().isAuthenticated).toBe(true)
  })

  it('rejects a wrong password without creating a session', async () => {
    const auth = useAuthStore()
    await expect(auth.login('admin', 'wrong-password')).rejects.toBeInstanceOf(ApiError)

    expect(auth.isAuthenticated).toBe(false)
    expect(localStorage.getItem('nerves.admin.session.v1')).toBeNull()
  })

  it('grants read-only permissions to the auditor account', async () => {
    const auth = useAuthStore()
    await auth.login('auditor', 'nerves2026')

    expect(auth.can('audit:event:read')).toBe(true)
    expect(auth.can('audit:event:export')).toBe(true)
    expect(auth.can('identity:account:write')).toBe(false)
    expect(auth.can('directory:application:write')).toBe(false)
  })

  it('clears the session on logout and on a 401', async () => {
    const auth = useAuthStore()
    await auth.login('admin', 'nerves2026')

    auth.logout({ revoke: false })
    expect(auth.isAuthenticated).toBe(false)
    expect(localStorage.getItem('nerves.admin.session.v1')).toBeNull()

    await auth.login('admin', 'nerves2026')
    auth.handleUnauthorized()
    expect(auth.isAuthenticated).toBe(false)
    expect(localStorage.getItem('nerves.admin.session.v1')).toBeNull()
  })
})
