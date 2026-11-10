import { flushPromises, mount } from '@vue/test-utils'
import { beforeEach, describe, expect, it } from 'vitest'
import { createMemoryHistory } from 'vue-router'

import App from '@/App.vue'
import { getApi, setApi } from '@/api'
import type { OrgRole } from '@/api/types'
import { i18n } from '@/i18n'
import { createAppRouter } from '@/router'
import { useAuthStore } from '@/stores/auth'
import AccountListView from '@/views/accounts/AccountListView.vue'
import LoginView from '@/views/LoginView.vue'
import NotificationListView from '@/views/notifications/NotificationListView.vue'
import OrganizationListView from '@/views/organizations/OrganizationListView.vue'
import { authContext, freshPinia, testApi } from './helpers'

async function mountWithApp(component: Parameters<typeof mount>[0], path: string) {
  const pinia = freshPinia()
  const router = createAppRouter(() => authContext(), createMemoryHistory())
  await router.push(path)
  await router.isReady()

  const wrapper = mount(component, {
    global: {
      plugins: [pinia, i18n, router],
    },
  })
  await flushPromises()
  return { wrapper, router }
}

describe('views', () => {
  beforeEach(() => {
    localStorage.clear()
    setApi(testApi())
  })

  it('renders the login page and reports bad credentials', async () => {
    const { wrapper } = await mountWithApp(LoginView, '/login')

    expect(wrapper.text()).toContain('登录管理后台')

    const inputs = wrapper.findAll('input')
    await inputs[0]?.setValue('admin')
    await inputs[1]?.setValue('definitely-wrong')
    await wrapper.find('form').trigger('submit')
    await flushPromises()

    expect(wrapper.text()).toContain('用户名或密码不正确')
    expect(wrapper.find('[role="alert"]').exists()).toBe(true)
  })

  it('logs in through the form and navigates to the overview', async () => {
    const { wrapper, router } = await mountWithApp(LoginView, '/login')

    const inputs = wrapper.findAll('input')
    await inputs[0]?.setValue('admin')
    await inputs[1]?.setValue('nerves2026')
    await wrapper.find('form').trigger('submit')
    await flushPromises()

    expect(useAuthStore().isAuthenticated).toBe(true)
    expect(router.currentRoute.value.name).toBe('dashboard')
  })

  it('renders the account table from the API layer', async () => {
    const pinia = freshPinia()
    const auth = useAuthStore()
    await auth.login('admin', 'nerves2026')

    const router = createAppRouter(() => ({ isAuthenticated: true, hasPermission: () => true }), createMemoryHistory())
    await router.push('/accounts')
    await router.isReady()

    const wrapper = mount(AccountListView, {
      global: { plugins: [pinia, i18n, router] },
    })
    await flushPromises()

    const rows = wrapper.findAll('tbody tr')
    expect(rows.length).toBeGreaterThanOrEqual(10)
    expect(wrapper.text()).toContain('张伟')
    expect(
      wrapper.findAll('a').some((link) => /\/accounts\/[\w-]+/.test(link.attributes('href') ?? '')),
    ).toBe(true)

    // The disable action is visible for an active account and opens the reason dialog.
    const disableButtons = wrapper.findAll('button').filter((button) => button.text() === '停用')
    expect(disableButtons.length).toBeGreaterThan(0)
    await disableButtons[0]?.trigger('click')
    await flushPromises()
    expect(document.body.textContent).toContain('停用账户')
  })

  it('boots the shell and navigates between pages', async () => {
    const pinia = freshPinia()
    setApi(testApi())
    await useAuthStore().login('admin', 'nerves2026')

    const router = createAppRouter(
      () => ({ isAuthenticated: true, hasPermission: () => true }),
      createMemoryHistory(),
    )
    await router.push('/')
    await router.isReady()

    const wrapper = mount(App, { global: { plugins: [pinia, i18n, router] } })
    await flushPromises()

    expect(wrapper.text()).toContain('Nerves 账户中心')
    expect(wrapper.text()).toContain('概览')
    expect(wrapper.text()).toContain('账户总数')
    expect(wrapper.find('button[aria-label="退出登录"]').exists()).toBe(true)

    await router.push('/audit')
    await flushPromises()
    expect(wrapper.text()).toContain('审计日志')
    expect(wrapper.text()).toContain('导出 CSV')

    await router.push('/applications')
    await flushPromises()
    expect(wrapper.text()).toContain('应用目录')
    expect(wrapper.text()).toContain('nerves-console')
  })

  it('changes an organization member role through the select control', async () => {
    const pinia = freshPinia()
    setApi(testApi())
    await useAuthStore().login('admin', 'nerves2026')

    const api = getApi()
    const organizations = await api.listOrganizations()
    const organization = organizations[0]
    expect(organization).toBeDefined()
    if (!organization) return

    const before = await api.listOrganizationMembers(organization.id)
    const member = before[0]
    expect(member).toBeDefined()
    if (!member) return

    const roles: OrgRole[] = ['owner', 'admin', 'member', 'viewer']
    const target = roles.find((role) => role !== member.orgRole) ?? 'viewer'

    const router = createAppRouter(() => ({ isAuthenticated: true, hasPermission: () => true }), createMemoryHistory())
    await router.push('/organizations')
    await router.isReady()

    const wrapper = mount(OrganizationListView, { global: { plugins: [pinia, i18n, router] } })
    await flushPromises()

    const roleSelects = wrapper.findAll('tbody select')
    expect(roleSelects.length).toBeGreaterThan(0)
    await roleSelects[0]?.setValue(target)
    await flushPromises()

    const after = await api.listOrganizationMembers(organization.id)
    expect(after.find((item) => item.id === member.id)?.orgRole).toBe(target)
  })

  it('lists notifications and sends one from the notification center', async () => {
    const pinia = freshPinia()
    setApi(testApi())
    await useAuthStore().login('admin', 'nerves2026')
    const api = getApi()

    const router = createAppRouter(() => ({ isAuthenticated: true, hasPermission: () => true }), createMemoryHistory())
    await router.push('/notifications')
    await router.isReady()

    // The send dialog teleports to <body>; stabilise it in-tree for assertions.
    const wrapper = mount(NotificationListView, {
      global: { plugins: [pinia, i18n, router], stubs: { teleport: true } },
    })
    await flushPromises()

    expect(wrapper.text()).toContain('通知')
    const rows = wrapper.findAll('tbody tr')
    expect(rows.length).toBeGreaterThan(1)
    expect(wrapper.text()).toContain('partner@example.com')

    await wrapper.findAll('select')[0]?.setValue('failed')
    // The list reload is debounced to keep typing cheap; wait past the window.
    await new Promise((resolve) => setTimeout(resolve, 350))
    await flushPromises()
    expect(wrapper.text()).not.toContain('liu.yang@nerves.example')
    expect(wrapper.text()).toContain('partner@example.com')

    const sendButton = wrapper.findAll('button').find((button) => button.text().includes('发送通知'))
    expect(sendButton).toBeDefined()
    await sendButton?.trigger('click')
    await flushPromises()

    await wrapper.find('#notification-to').setValue('new-recipient@example.com')
    await wrapper.find('#notification-value-displayName').setValue('新用户')
    await wrapper.find('#notification-value-username').setValue('new.user')
    await wrapper.find('#notification-value-issuer').setValue('https://nerves.example.com/')

    const submit = wrapper.findAll('button').find((button) => button.text() === '发送')
    expect(submit).toBeDefined()
    await submit?.trigger('click')
    await flushPromises()

    const page = await api.listNotifications({ limit: 20, to: 'new-recipient@example.com' })
    expect(page.items).toHaveLength(1)
    expect(page.items[0]?.status).toBe('sent')
  })
})
