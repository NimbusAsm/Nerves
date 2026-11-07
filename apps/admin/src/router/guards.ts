import type { RouteLocationNormalized, RouteLocationRaw } from 'vue-router'

export interface AuthContext {
  isAuthenticated: boolean
  hasPermission: (permission: string) => boolean
}

/**
 * Login gate + per-route permission gate. Pure enough to unit test: the store is
 * read lazily so the guard never holds a stale snapshot.
 */
export function createAuthGuard(getAuth: () => AuthContext) {
  return (to: RouteLocationNormalized): true | RouteLocationRaw => {
    const auth = getAuth()

    if (to.meta.public === true) {
      if (to.name === 'login' && auth.isAuthenticated) return { name: 'dashboard' }
      return true
    }

    if (!auth.isAuthenticated) {
      const redirect = to.fullPath === '/' ? {} : { redirect: to.fullPath }
      return to.name === 'login' ? true : { name: 'login', query: redirect }
    }

    const permission = to.meta.permission
    if (permission && !auth.hasPermission(permission)) return { name: 'forbidden' }

    return true
  }
}
