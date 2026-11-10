import { beforeEach, describe, expect, it } from 'vitest'

import { ApiError } from '@/api'
import type { NervesApi } from '@/api/contract'
import { testApi } from './helpers'

const WELCOME = {
  displayName: '张伟',
  username: 'zhang.wei',
  issuer: 'https://nerves.example.com/',
}

describe('mock notification API', () => {
  let api: NervesApi

  beforeEach(() => {
    api = testApi()
  })

  it('pages the delivery log with opaque cursors', async () => {
    const first = await api.listNotifications({ limit: 3 })
    expect(first.items).toHaveLength(3)
    expect(first.nextCursor).toBeTruthy()

    const second = await api.listNotifications({ limit: 3, cursor: first.nextCursor })
    const firstIds = new Set(first.items.map((item) => item.id))
    expect(second.items.some((item) => firstIds.has(item.id))).toBe(false)
  })

  it('sorts newest first', async () => {
    const page = await api.listNotifications({ limit: 10 })
    const timestamps = page.items.map((item) => item.createdAt)
    expect([...timestamps].sort().reverse()).toEqual(timestamps)
  })

  it('filters by status and recipient', async () => {
    const failed = await api.listNotifications({ limit: 50, status: 'failed' })
    expect(failed.items.length).toBeGreaterThan(0)
    expect(failed.items.every((item) => item.status === 'failed')).toBe(true)

    const recipient = failed.items[0]?.to ?? ''
    const byRecipient = await api.listNotifications({ limit: 50, to: recipient })
    expect(byRecipient.items.every((item) => item.to === recipient)).toBe(true)
  })

  it('queues a templated email and exposes it in the log', async () => {
    const created = await api.sendNotificationEmail({
      to: 'smoke@example.com',
      template: 'account.welcome',
      values: WELCOME,
    })

    expect(created.status).toBe('sent')
    expect(created.to).toBe('smoke@example.com')
    expect(created.subject).toContain('张伟')
    expect(created.sentAt).toBeTruthy()

    const fetched = await api.getNotification(created.id)
    expect(fetched.id).toBe(created.id)

    const page = await api.listNotifications({ limit: 10, to: 'smoke@example.com' })
    expect(page.items.some((item) => item.id === created.id)).toBe(true)
  })

  it('rejects invalid recipients, unknown templates and missing values', async () => {
    await expect(
      api.sendNotificationEmail({ to: 'not-an-address', template: 'account.welcome', values: WELCOME }),
    ).rejects.toBeInstanceOf(ApiError)

    await expect(
      api.sendNotificationEmail({ to: 'a@example.com', template: 'unknown', values: {} }),
    ).rejects.toBeInstanceOf(ApiError)

    await expect(
      api.sendNotificationEmail({ to: 'a@example.com', template: 'account.password-reset', values: { displayName: 'Ada' } }),
    ).rejects.toMatchObject({ status: 400 })
  })

  it('returns 404 for an unknown notification', async () => {
    await expect(api.getNotification('ntf_missing')).rejects.toMatchObject({ status: 404 })
  })
})
