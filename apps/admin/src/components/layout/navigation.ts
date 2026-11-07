import { AppWindow, Building, Landmark, LayoutDashboard, ScrollText, ShieldCheck, Users } from '@lucide/vue'
import type { Component } from 'vue'

export interface NavItem {
  /** Route name. */
  name: string
  /** Path prefix used to keep the item highlighted on detail routes. */
  path: string
  labelKey: string
  icon: Component
  permission?: string
}

export interface NavGroup {
  labelKey: string | null
  items: NavItem[]
}

export const NAV_GROUPS: NavGroup[] = [
  {
    labelKey: null,
    items: [{ name: 'dashboard', path: '/', labelKey: 'nav.overview', icon: LayoutDashboard, permission: 'stats:read' }],
  },
  {
    labelKey: 'nav.sections.identity',
    items: [
      { name: 'accounts', path: '/accounts', labelKey: 'nav.accounts', icon: Users, permission: 'identity:account:read' },
      { name: 'legal-entities', path: '/legal-entities', labelKey: 'nav.legalEntities', icon: Landmark, permission: 'identity:legal-entity:read' },
    ],
  },
  {
    labelKey: 'nav.sections.access',
    items: [
      { name: 'organizations', path: '/organizations', labelKey: 'nav.organizations', icon: Building, permission: 'org:organization:read' },
      { name: 'roles', path: '/roles', labelKey: 'nav.roles', icon: ShieldCheck, permission: 'authz:role:read' },
    ],
  },
  {
    labelKey: 'nav.sections.directory',
    items: [
      { name: 'applications', path: '/applications', labelKey: 'nav.applications', icon: AppWindow, permission: 'directory:application:read' },
    ],
  },
  {
    labelKey: 'nav.sections.security',
    items: [{ name: 'audit', path: '/audit', labelKey: 'nav.audit', icon: ScrollText, permission: 'audit:event:read' }],
  },
]
