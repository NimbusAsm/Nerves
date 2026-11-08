import { beforeEach, describe, expect, it } from 'vitest'

import type { NervesApi } from '@/api/contract'
import { testApi } from './helpers'

describe('mock passkeys', () => {
  let api: NervesApi

  beforeEach(() => {
    api = testApi()
  })

  it('returns registration options with a challenge', async () => {
    const account = (await api.listAccounts({ limit: 1 })).items[0]!

    const registration = await api.startPasskeyRegistration(account.id)

    expect(registration.challengeId).toBeTruthy()
    expect(registration.options.challenge).toBeTruthy()
    expect(registration.options.rp.id).toBe('nerves.local')
  })

  it('registers and then removes a passkey', async () => {
    const account = (await api.listAccounts({ limit: 1 })).items[0]!
    const registration = await api.startPasskeyRegistration(account.id)

    const result = await api.completePasskeyRegistration(account.id, registration.challengeId, {})
    expect(result.active).toBe(true)

    const detail = await api.getAccount(account.id)
    expect(detail.credentials.some((credential) => credential.type === 'passkey')).toBe(true)

    await api.removePasskeys(account.id)
    const after = await api.getAccount(account.id)
    expect(after.credentials.some((credential) => credential.type === 'passkey')).toBe(false)
  })
})
