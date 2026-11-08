import { defineStore } from 'pinia'
import { computed, ref } from 'vue'

import { apiMode, getApi } from '@/api'
import { beginLogin, completeLogin } from '@/api/oidc'
import {
  clearStoredSession,
  readStoredSession,
  writeStoredSession,
  type StoredSession,
} from '@/api/session-storage'

/**
 * The console never keeps tokens in storage in live mode: the IdP issues a
 * short-lived access token that lives in memory, and a reload runs the OIDC
 * flow again. Mock mode keeps the token so the demo survives reloads.
 */
export const useAuthStore = defineStore('auth', () => {
  const session = ref<StoredSession | null>(apiMode === 'mock' ? readStoredSession() : null)

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

  function setSession(next: StoredSession): void {
    session.value = next
    if (apiMode === 'mock') writeStoredSession(next)
  }

  /** Live mode hands off to the IdP and leaves the page; mock logs in locally. */
  async function login(username: string, password: string, redirectTo = '/'): Promise<void> {
    if (apiMode !== 'mock') {
      await beginLogin(username, password, redirectTo)
      return
    }

    const result = await getApi().login({ username, password })
    setSession({ token: result.token, user: result.user })
  }

  /** Completes the authorization code exchange after the IdP redirect. */
  async function finishLogin(search: string): Promise<string> {
    const result = await completeLogin(search)
    setSession({ token: result.token, user: result.user })
    return result.redirectTo
  }

  function logout(options: { revoke?: boolean } = {}): void {
    session.value = null
    clearStoredSession()
    if (options.revoke !== false && apiMode === 'mock') {
      void getApi().logout().catch(() => undefined)
    }
  }

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
    finishLogin,
    logout,
    handleUnauthorized,
  }
})
