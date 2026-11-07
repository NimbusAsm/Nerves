import type { NervesUser } from './types'

const STORAGE_KEY = 'nerves.admin.session.v1'

export interface StoredSession {
  token: string
  user: NervesUser
}

export function readStoredSession(): StoredSession | null {
  if (typeof localStorage === 'undefined') return null
  try {
    const raw = localStorage.getItem(STORAGE_KEY)
    if (!raw) return null
    const parsed = JSON.parse(raw) as Partial<StoredSession>
    if (typeof parsed.token !== 'string' || !parsed.token || !parsed.user) return null
    return { token: parsed.token, user: parsed.user }
  } catch {
    return null
  }
}

export function readStoredToken(): string | null {
  return readStoredSession()?.token ?? null
}

export function writeStoredSession(session: StoredSession): void {
  if (typeof localStorage === 'undefined') return
  localStorage.setItem(STORAGE_KEY, JSON.stringify(session))
}

export function clearStoredSession(): void {
  if (typeof localStorage === 'undefined') return
  localStorage.removeItem(STORAGE_KEY)
}

export const sessionStorageKey = STORAGE_KEY
