import { beforeEach, describe, expect, it } from 'vitest'

import type { NervesApi } from '@/api/contract'
import { testApi } from './helpers'

describe('mock integration settings', () => {
  let api: NervesApi

  beforeEach(() => {
    api = testApi()
  })

  it('applies a partial LDAP update', async () => {
    const updated = await api.updateLdapSettings({ enabled: true, host: 'ldap.example.com', port: 636 })

    expect(updated.enabled).toBe(true)
    expect(updated.host).toBe('ldap.example.com')
    expect(updated.port).toBe(636)
  })

  it('keeps PDP fail-closed when it is enabled', async () => {
    const updated = await api.updatePdpSettings({ enabled: true, url: 'https://pdp.example.com/decide' })

    expect(updated.enabled).toBe(true)
    expect(updated.url).toBe('https://pdp.example.com/decide')
    expect(updated.failOpen).toBe(false)
  })
})
