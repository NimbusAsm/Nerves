import { beforeEach, describe, expect, it } from 'vitest'

import { ApiError } from '@/api'
import type { NervesApi } from '@/api/contract'
import { testApi } from './helpers'

describe('mock API', () => {
  let api: NervesApi

  beforeEach(() => {
    api = testApi()
  })

  it('pages accounts with opaque cursors and no overlap', async () => {
    const first = await api.listAccounts({ limit: 5 })
    expect(first.items).toHaveLength(5)
    expect(first.nextCursor).toBeTruthy()

    const second = await api.listAccounts({ limit: 5, cursor: first.nextCursor })
    expect(second.items).toHaveLength(5)

    const firstIds = new Set(first.items.map((account) => account.id))
    expect(second.items.some((account) => firstIds.has(account.id))).toBe(false)
  })

  it('filters accounts by status and search term', async () => {
    const disabled = await api.listAccounts({ limit: 100, status: 'disabled' })
    expect(disabled.items.length).toBeGreaterThan(0)
    expect(disabled.items.every((account) => account.status === 'disabled')).toBe(true)

    const page = await api.listAccounts({ limit: 100 })
    const sample = page.items[0]
    expect(sample).toBeDefined()
    const searched = await api.listAccounts({ limit: 100, q: sample?.username ?? '' })
    expect(searched.items.some((account) => account.id === sample?.id)).toBe(true)
  })

  it('requires a reason before disabling an account and audits the change', async () => {
    const page = await api.listAccounts({ limit: 100, status: 'active' })
    const account = page.items[0]
    expect(account).toBeDefined()
    if (!account) return

    await expect(api.setAccountStatus(account.id, 'disabled')).rejects.toBeInstanceOf(ApiError)

    const updated = await api.setAccountStatus(account.id, 'disabled', '安全事件应急响应')
    expect(updated.status).toBe('disabled')

    const detail = await api.getAccount(account.id)
    expect(detail.status).toBe('disabled')
    expect(detail.disabledReason).toBe('安全事件应急响应')

    const audit = await api.listAccountAudit(account.id, { limit: 10 })
    expect(audit.items.some((event) => event.action === 'account.disabled')).toBe(true)
  })

  it('includes the legal entity with an account detail', async () => {
    const page = await api.listAccounts({ limit: 1 })
    const account = page.items[0]
    expect(account).toBeDefined()
    if (!account) return

    const detail = await api.getAccount(account.id)
    expect(detail.legalEntity.id).toBe(account.legalEntityId)
    expect(detail.legalEntity.idDocType).toBeTruthy()
    expect(detail.credentials.some((credential) => credential.type === 'password')).toBe(true)
  })

  it('filters audit events by result and actor', async () => {
    const failures = await api.listAuditEvents({ limit: 100, result: 'failure' })
    expect(failures.items.length).toBeGreaterThan(0)
    expect(failures.items.every((event) => event.result === 'failure')).toBe(true)

    const actor = failures.items[0]?.actorName ?? ''
    const byActor = await api.listAuditEvents({ limit: 100, actor })
    expect(byActor.items.every((event) => event.actorName.includes(actor))).toBe(true)
  })

  it('validates applications before creating them', async () => {
    const existing = await api.listApplications()
    const existingClientId = existing[0]?.clientId ?? 'nerves-console'

    await expect(
      api.createApplication({
        name: '重复应用',
        clientId: existingClientId,
        description: '',
        logoUrl: null,
        redirectUris: ['https://app.example.com/callback'],
        scopes: ['openid'],
      }),
    ).rejects.toMatchObject({ status: 409 })

    await expect(
      api.createApplication({
        name: '坏回调',
        clientId: 'bad-callback-app',
        description: '',
        logoUrl: null,
        redirectUris: ['http://insecure.example.com/callback'],
        scopes: ['openid'],
      }),
    ).rejects.toMatchObject({ status: 400 })

    const created = await api.createApplication({
      name: '新应用',
      clientId: 'new-portal',
      description: '用于测试',
      logoUrl: null,
      redirectUris: ['https://portal.example.com/oauth/callback'],
      scopes: ['openid', 'profile'],
    })

    expect(created.clientId).toBe('new-portal')
    const applications = await api.listApplications()
    expect(applications.some((application) => application.id === created.id)).toBe(true)

    const audit = await api.listAuditEvents({ limit: 20, action: 'application.created' })
    expect(audit.items.some((event) => event.targetId === created.id)).toBe(true)
  })

  it('keeps the dashboard statistics consistent with the dataset', async () => {
    const stats = await api.getStats()
    const accounts = await api.listAccounts({ limit: 100 })

    expect(stats.totalAccounts).toBe(accounts.items.length)
    expect(stats.activeAccounts + stats.lockedAccounts + stats.disabledAccounts).toBe(stats.totalAccounts)
    expect(stats.loginTrend).toHaveLength(7)

    const today = stats.loginTrend.at(-1)
    expect(today?.success).toBe(stats.todayLogins)
    expect(today?.failed).toBe(stats.todayFailedLogins)
  })

  it('updates organization membership', async () => {
    const organizations = await api.listOrganizations()
    const organization = organizations[0]
    expect(organization).toBeDefined()
    if (!organization) return

    const members = await api.listOrganizationMembers(organization.id)
    const member = members[0]
    expect(member).toBeDefined()
    if (!member) return

    const updated = await api.updateOrganizationMember(organization.id, member.id, 'viewer')
    expect(updated.orgRole).toBe('viewer')

    const after = await api.listOrganizationMembers(organization.id)
    expect(after.find((item) => item.id === member.id)?.orgRole).toBe('viewer')
  })

  it('rejects malformed cursors', async () => {
    await expect(api.listAccounts({ cursor: 'not-a-cursor' })).rejects.toBeInstanceOf(ApiError)
  })
})
