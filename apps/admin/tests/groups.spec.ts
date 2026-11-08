import { beforeEach, describe, expect, it } from 'vitest'

import type { NervesApi } from '@/api/contract'
import { testApi } from './helpers'

describe('mock groups', () => {
  let api: NervesApi

  beforeEach(() => {
    api = testApi()
  })

  it('lists seeded groups with a member count', async () => {
    const groups = await api.listGroups()

    expect(groups.length).toBeGreaterThan(0)
    expect(groups.every((group) => typeof group.memberCount === 'number')).toBe(true)
  })

  it('creates and deletes a group', async () => {
    const created = await api.createGroup({ realm: 'nerves', code: 'qa', name: 'QA' })

    expect((await api.listGroups()).some((group) => group.id === created.id)).toBe(true)

    await api.deleteGroup(created.id)
    expect((await api.listGroups()).some((group) => group.id === created.id)).toBe(false)
  })

  it('adds and removes a member', async () => {
    const group = (await api.listGroups())[0]!
    const account = (await api.listAccounts({ limit: 1 })).items[0]!

    await api.addGroupMember(group.id, account.id)
    expect((await api.listGroupMembers(group.id)).some((member) => member.accountId === account.id)).toBe(true)

    await api.removeGroupMember(group.id, account.id)
    expect((await api.listGroupMembers(group.id)).some((member) => member.accountId === account.id)).toBe(false)
  })

  it('rejects a duplicate group code', async () => {
    await expect(api.createGroup({ realm: 'nerves', code: 'engineering', name: 'Dup' })).rejects.toThrow()
  })
})
