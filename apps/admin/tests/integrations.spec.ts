import { beforeEach, describe, expect, it } from 'vitest'

import type { NervesApi } from '@/api/contract'
import { testApi } from './helpers'

describe('mock integration status', () => {
  let api: NervesApi

  beforeEach(() => {
    api = testApi()
  })

  it('reports LDAP and federation configuration', async () => {
    const status = await api.getIntegrationStatus()

    expect(status.ldap).toBeDefined()
    expect(typeof status.ldap.enabled).toBe('boolean')
    expect(Array.isArray(status.federation)).toBe(true)
    expect(status.ldap.userFilter).toContain('{0}')
  })

  it('reports a fail-closed PDP by default', async () => {
    const pdp = await api.getPdpStatus()

    expect(pdp.failOpen).toBe(false)
    expect(pdp.timeoutSeconds).toBeGreaterThan(0)
  })
})
