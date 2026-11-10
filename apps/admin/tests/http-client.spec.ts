import { describe, expect, it } from 'vitest'

import { createHttpApi } from '@/api/http'
import type { NotificationRecord } from '@/api/types'

interface Captured {
  url: string
  init: RequestInit
}

function fakeFetch(calls: Captured[], body: unknown = { items: [], nextCursor: null }): typeof fetch {
  return (async (input: RequestInfo | URL, init?: RequestInit) => {
    calls.push({ url: String(input), init: init ?? {} })
    return new Response(JSON.stringify(body), {
      status: 200,
      headers: { 'Content-Type': 'application/json' },
    })
  }) as typeof fetch
}

const record: NotificationRecord = {
  id: 'ntf_1',
  to: 'a@example.com',
  template: 'account.welcome',
  subject: 'welcome',
  status: 'sent',
  attempts: 0,
  error: null,
  createdAt: '2026-11-10T10:00:00Z',
  sentAt: '2026-11-10T10:00:01Z',
  nextAttemptAt: '2026-11-10T10:00:00Z',
}

describe('HTTP api client', () => {
  it('does not request credentials the gateway CORS policy would reject', async () => {
    const calls: Captured[] = []
    const api = createHttpApi({
      baseUrl: '/api/v1',
      getToken: () => 'token-1',
      fetchImpl: fakeFetch(calls),
    })

    await api.listNotifications({ limit: 5 })

    expect(calls).toHaveLength(1)
    expect(calls[0]?.init.credentials).toBe('omit')
    expect((calls[0]?.init.headers as Headers).get('Authorization')).toBe('Bearer token-1')
  })

  it('maps the notification endpoints onto the service routes', async () => {
    const calls: Captured[] = []
    const api = createHttpApi({
      baseUrl: '/api/v1',
      getToken: () => null,
      fetchImpl: fakeFetch(calls, record),
    })

    await api.sendNotificationEmail({
      to: 'a@example.com',
      template: 'account.welcome',
      values: { displayName: 'Ada' },
    })
    await api.getNotification('ntf_1')

    expect(calls[0]?.url).toBe('/api/v1/notifications/email')
    expect(calls[0]?.init.method).toBe('POST')
    expect(JSON.parse(String(calls[0]?.init.body))).toEqual({
      to: 'a@example.com',
      template: 'account.welcome',
      values: { displayName: 'Ada' },
    })
    expect(calls[1]?.url).toBe('/api/v1/notifications/ntf_1')
  })

  it('builds notification list filters as query parameters', async () => {
    const calls: Captured[] = []
    const api = createHttpApi({ baseUrl: '/api/v1', getToken: () => null, fetchImpl: fakeFetch(calls) })

    await api.listNotifications({ limit: 10, cursor: 'abc', status: 'failed', to: 'a@example.com' })

    const url = new URL(String(calls[0]?.url), 'http://localhost')
    expect(url.pathname).toBe('/api/v1/notifications')
    expect(url.searchParams.get('limit')).toBe('10')
    expect(url.searchParams.get('cursor')).toBe('abc')
    expect(url.searchParams.get('status')).toBe('failed')
    expect(url.searchParams.get('to')).toBe('a@example.com')
  })
})
