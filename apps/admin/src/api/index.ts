import { createHttpApi } from './http'
import { createMockApi } from './mock'
import type { NervesApi } from './contract'
import { getAccessToken } from './token'

/**
 * Mock is the default so the console is usable before the services exist.
 * Point it at the real gateway with `VITE_USE_MOCK=false` + `VITE_API_BASE_URL`.
 */
const mockEnabled = (import.meta.env.VITE_USE_MOCK ?? 'true').trim().toLowerCase() !== 'false'

export const apiMode: 'mock' | 'live' = mockEnabled ? 'mock' : 'live'

function createDefaultApi(): NervesApi {
  if (mockEnabled) return createMockApi()

  return createHttpApi({
    baseUrl: import.meta.env.VITE_API_BASE_URL ?? '/api/v1',
    getToken: getAccessToken,
    onUnauthorized: () => {
      if (typeof window !== 'undefined') window.dispatchEvent(new CustomEvent('nerves:unauthorized'))
    },
  })
}

let current: NervesApi = createDefaultApi()

/** The single API instance every store and view uses. */
export function getApi(): NervesApi {
  return current
}

/** Swap the implementation — used by tests and by hosts that embed the console. */
export function setApi(next: NervesApi): void {
  current = next
}

export { ApiError, describeError, isApiError } from './http'
export type { NervesApi } from './contract'
export type * from './types'
