import { createRouter, createWebHashHistory, type RouterHistory } from 'vue-router'
import type { RouteRecordRaw } from 'vue-router'

import AdminShell from '@/components/layout/AdminShell.vue'
import { i18n } from '@/i18n'
import { useAuthStore } from '@/stores/auth'
import { createAuthGuard, type AuthContext } from './guards'

declare module 'vue-router' {
  interface RouteMeta {
    /** Reachable without a session (login). */
    public?: boolean
    /** Permission code required to enter the route. */
    permission?: string
    /** i18n key rendered in the top bar and document title. */
    titleKey?: string
    /** i18n key of the navigation section shown above the title. */
    sectionKey?: string
  }
}

export const routes: RouteRecordRaw[] = [
  {
    path: '/auth/callback',
    name: 'auth-callback',
    component: () => import('@/views/AuthCallbackView.vue'),
    meta: { public: true },
  },
  {
    path: '/login',
    name: 'login',
    component: () => import('@/views/LoginView.vue'),
    meta: { public: true, titleKey: 'auth.title' },
  },
  {
    path: '/',
    component: AdminShell,
    children: [
      {
        path: '',
        name: 'dashboard',
        component: () => import('@/views/DashboardView.vue'),
        meta: { titleKey: 'dashboard.title', permission: 'stats:read' },
      },
      {
        path: 'accounts',
        name: 'accounts',
        component: () => import('@/views/accounts/AccountListView.vue'),
        meta: {
          titleKey: 'accounts.title',
          sectionKey: 'nav.sections.identity',
          permission: 'identity:account:read',
        },
      },
      {
        path: 'accounts/:id',
        name: 'account-detail',
        component: () => import('@/views/accounts/AccountDetailView.vue'),
        meta: {
          titleKey: 'accounts.detailTitle',
          sectionKey: 'nav.sections.identity',
          permission: 'identity:account:read',
        },
      },
      {
        path: 'legal-entities',
        name: 'legal-entities',
        component: () => import('@/views/legal-entities/LegalEntityListView.vue'),
        meta: {
          titleKey: 'entities.title',
          sectionKey: 'nav.sections.identity',
          permission: 'identity:legal-entity:read',
        },
      },
      {
        path: 'legal-entities/:id',
        name: 'legal-entity-detail',
        component: () => import('@/views/legal-entities/LegalEntityDetailView.vue'),
        meta: {
          titleKey: 'entities.detailTitle',
          sectionKey: 'nav.sections.identity',
          permission: 'identity:legal-entity:read',
        },
      },
      {
        path: 'organizations',
        name: 'organizations',
        component: () => import('@/views/organizations/OrganizationListView.vue'),
        meta: {
          titleKey: 'orgs.title',
          sectionKey: 'nav.sections.access',
          permission: 'org:organization:read',
        },
      },
      {
        path: 'groups',
        name: 'groups',
        component: () => import('@/views/groups/GroupListView.vue'),
        meta: {
          titleKey: 'groups.title',
          sectionKey: 'nav.sections.access',
          permission: 'authz:role:read',
        },
      },
      {
        path: 'roles',
        name: 'roles',
        component: () => import('@/views/roles/RoleListView.vue'),
        meta: {
          titleKey: 'roles.title',
          sectionKey: 'nav.sections.access',
          permission: 'authz:role:read',
        },
      },
      {
        path: 'audit',
        name: 'audit',
        component: () => import('@/views/audit/AuditLogView.vue'),
        meta: {
          titleKey: 'audit.title',
          sectionKey: 'nav.sections.security',
          permission: 'audit:event:read',
        },
      },
      {
        path: 'integrations',
        name: 'integrations',
        component: () => import('@/views/integrations/IntegrationsView.vue'),
        meta: {
          titleKey: 'integrations.title',
          sectionKey: 'nav.sections.directory',
          permission: 'authz:role:read',
        },
      },
      {
        path: 'applications',
        name: 'applications',
        component: () => import('@/views/applications/ApplicationListView.vue'),
        meta: {
          titleKey: 'apps.title',
          sectionKey: 'nav.sections.directory',
          permission: 'directory:application:read',
        },
      },
      {
        path: 'forbidden',
        name: 'forbidden',
        component: () => import('@/views/ForbiddenView.vue'),
        meta: { titleKey: 'errors.forbiddenTitle' },
      },
      {
        path: ':pathMatch(.*)*',
        name: 'not-found',
        component: () => import('@/views/NotFoundView.vue'),
        meta: { titleKey: 'errors.notFoundTitle' },
      },
    ],
  },
]

export function createAppRouter(getAuth: () => AuthContext, history?: RouterHistory) {
  const router = createRouter({
    history: history ?? createWebHashHistory(),
    routes,
    scrollBehavior: () => ({ top: 0 }),
  })

  router.beforeEach(createAuthGuard(getAuth))

  router.afterEach((to) => {
    if (typeof document === 'undefined') return
    const key = to.meta.titleKey
    const title = key ? i18n.global.t(key) : i18n.global.t('app.name')
    document.title = `${title} · ${i18n.global.t('app.name')}`
  })

  return router
}

export const router = createAppRouter(() => {
  const auth = useAuthStore()
  return {
    isAuthenticated: auth.isAuthenticated,
    hasPermission: (permission: string) => auth.can(permission),
  }
})
