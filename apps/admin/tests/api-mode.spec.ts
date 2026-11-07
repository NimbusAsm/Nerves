import { afterEach, describe, expect, it, vi } from 'vitest'

// VITE_USE_MOCK is read once when the API module is first imported, so each
// case resets the module registry before loading it again.
describe('API mode switch', () => {
  afterEach(() => {
    vi.unstubAllEnvs()
    vi.resetModules()
  })

  it('defaults to the in-memory mock', async () => {
    vi.stubEnv('VITE_USE_MOCK', 'true')
    vi.resetModules()
    const api = await import('@/api')

    expect(api.apiMode).toBe('mock')
    expect(api.getApi().mode).toBe('mock')
  })

  it('switches to the HTTP client when VITE_USE_MOCK=false', async () => {
    vi.stubEnv('VITE_USE_MOCK', 'false')
    vi.stubEnv('VITE_API_BASE_URL', '/api/v1')
    vi.resetModules()
    const api = await import('@/api')

    expect(api.apiMode).toBe('live')
    expect(api.getApi().mode).toBe('live')
  })
})
