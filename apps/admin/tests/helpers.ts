import { createPinia, setActivePinia } from 'pinia'
import { createMemoryHistory, createRouter, type Router } from 'vue-router'

import { createMockApi } from '@/api/mock'
import type { NervesApi } from '@/api/contract'
import type { AuthContext } from '@/router/guards'
import { createAuthGuard } from '@/router/guards'

/** A predictable mock backend: no artificial latency, fixed clock. */
export function testApi(): NervesApi {
  return createMockApi({
    latencyMs: 0,
    now: () => new Date('2026-11-07T10:30:00Z'),
  })
}

export function freshPinia() {
  const pinia = createPinia()
  setActivePinia(pinia)
  return pinia
}

export function authContext(overrides: Partial<AuthContext> = {}): AuthContext {
  return {
    isAuthenticated: true,
    hasPermission: () => true,
    ...overrides,
  }
}

/** Small router with stubbed components; asserts guard behaviour without loading views. */
export function guardRouter(getAuth: () => AuthContext): Router {
  const Stub = { template: '<div />' }
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/login', name: 'login', component: Stub, meta: { public: true } },
      { path: '/', name: 'dashboard', component: Stub, meta: { permission: 'stats:read' } },
      { path: '/accounts', name: 'accounts', component: Stub, meta: { permission: 'identity:account:read' } },
      { path: '/forbidden', name: 'forbidden', component: Stub, meta: {} },
      { path: '/audit', name: 'audit', component: Stub, meta: { permission: 'audit:event:read' } },
    ],
  })
  router.beforeEach(createAuthGuard(getAuth))
  return router
}
