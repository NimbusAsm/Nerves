import { defineStore } from 'pinia'
import { computed, ref } from 'vue'

import { getApi } from '@/api'
import {
  clearStoredSession,
  readStoredSession,
  writeStoredSession,
  type StoredSession,
} from '@/api/session-storage'

export const useAuthStore = defineStore('auth', () => {
  const session = ref<StoredSession | null>(readStoredSession())

  const token = computed(() => session.value?.token ?? null)
  const user = computed(() => session.value?.user ?? null)
  const isAuthenticated = computed(() => Boolean(session.value?.token))
  const roles = computed(() => user.value?.roles ?? [])
  const displayName = computed(() => user.value?.displayName ?? '')

  /** `*` grants everything; everything else is an exact permission code. */
  function can(permission?: string | null): boolean {
    if (!permission) return true
    const granted = user.value?.permissions ?? []
    return granted.includes('*') || granted.includes(permission)
  }

  async function login(username: string, password: string): Promise<void> {
    const result = await getApi().login({ username, password })
    session.value = { token: result.token, user: result.user }
    writeStoredSession(session.value)
  }

  function logout(options: { revoke?: boolean } = {}): void {
    const api = getApi()
    session.value = null
    clearStoredSession()
    if (options.revoke !== false) void api.logout().catch(() => undefined)
  }

  /** Called when the API answers 401: drop the local session without another round trip. */
  function handleUnauthorized(): void {
    session.value = null
    clearStoredSession()
  }

  return {
    session,
    token,
    user,
    roles,
    displayName,
    isAuthenticated,
    can,
    login,
    logout,
    handleUnauthorized,
  }
})
