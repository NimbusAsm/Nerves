import { expect, test } from '@playwright/test'

const username = process.env.ADMIN_USERNAME ?? 'admin'
const password = process.env.ADMIN_PASSWORD ?? 'ChangeMe!123'

async function signIn(page: import('@playwright/test').Page): Promise<void> {
  await page.goto('/')
  await expect(page.locator('#login-username')).toBeVisible()
  await page.locator('#login-username').fill(username)
  await page.locator('#login-password').fill(password)
  await page.getByRole('button', { name: /登录|sign in/i }).click()
  await page.waitForURL((url) => !url.pathname.startsWith('/login'), { timeout: 30_000 })
}

test('signs in through the IdP and reaches the console', async ({ page }) => {
  await signIn(page)

  await expect(page.locator('#login-username')).toHaveCount(0)
  await expect(page.getByRole('navigation').first()).toBeVisible()

  // The access token is held in memory only.
  const stored = await page.evaluate(() => Object.keys(localStorage).filter((key) => key.includes('token')))
  expect(stored).toEqual([])
})

test('lists legal entities after signing in', async ({ page }) => {
  await signIn(page)

  // Use in-app navigation: the access token lives in memory and a full page
  // load would sign the console out again.
  await page.getByRole('link', { name: /法人实体/ }).first().click()
  await expect(page).toHaveURL(/\/legal-entities/)
  await expect(page.getByRole('heading').first()).toBeVisible()
  await expect(page.locator('table')).toBeVisible()
})

test('manages the OIDC application catalogue from the sidebar', async ({ page }) => {
  await signIn(page)

  await page.getByRole('link', { name: /应用目录/ }).first().click()
  await expect(page).toHaveURL(/\/applications/)
  await expect(page.getByRole('heading', { name: '应用目录', exact: true })).toBeVisible()
  await expect(page.locator('table')).toBeVisible()

  // The list is backed by the IdP's OpenIddict registry, so the seeded
  // console client must be there and must be marked as built-in.
  const adminRow = page.locator('tr', { hasText: 'nerves-admin' })
  await expect(adminRow).toBeVisible()
  await expect(adminRow.getByText('内置')).toBeVisible()
  await expect(adminRow.getByRole('button', { name: /移除/ })).toHaveCount(0)

  const clientId = `e2e-app-${Date.now().toString(36)}`
  await page.getByRole('button', { name: /新建应用/ }).click()
  await expect(page.getByRole('dialog')).toBeVisible()
  await page.locator('#app-name').fill('E2E 契约应用')
  await page.locator('#app-client-id').fill(clientId)
  await page.locator('#app-callbacks').fill('https://e2e.example.com/callback')
  await page.locator('#app-post-logout').fill('https://e2e.example.com/')
  await page.getByRole('dialog').getByRole('button', { name: /^新建$/ }).click()

  await expect(page.getByRole('dialog')).toHaveCount(0)
  const createdRow = page.locator('tr', { hasText: clientId })
  await expect(createdRow).toContainText('E2E 契约应用')

  // Disable it: the status badge flips and the row keeps the same client id.
  await createdRow.getByRole('button', { name: /^编辑$/ }).click()
  await expect(page.getByRole('dialog')).toBeVisible()
  await page.locator('#edit-app-name')
  await page.getByRole('dialog').getByRole('checkbox').last().uncheck()
  await page.getByRole('dialog').getByRole('button', { name: /^保存$/ }).click()
  await expect(page.getByRole('dialog')).toHaveCount(0)
  await expect(page.locator('tr', { hasText: clientId })).toContainText('停用')

  // Deleting cleans up the registry again.
  await page.locator('tr', { hasText: clientId }).getByRole('button', { name: /移除/ }).click()
  await expect(page.getByRole('dialog')).toBeVisible()
  await page.getByRole('dialog').getByRole('button', { name: /^移除$/ }).click()
  await expect(page.locator('tr', { hasText: clientId })).toHaveCount(0)
})

test('opens the notification center from the sidebar and sends a template email', async ({ page }) => {
  await signIn(page)

  // In-app navigation only: the access token lives in memory, so a full page
  // load signs the console out and the sidebar disappears.
  await page.getByRole('link', { name: /^通知$/ }).click()
  await expect(page).toHaveURL(/\/notifications/)
  await expect(page.getByRole('heading', { name: /^通知$/ })).toBeVisible()
  await expect(page.locator('table')).toBeVisible()

  await page.getByRole('button', { name: /发送通知/ }).click()
  await expect(page.getByRole('dialog')).toBeVisible()
  await page.locator('#notification-to').fill('e2e@example.com')
  await page.locator('#notification-value-displayName').fill('E2E 用户')
  await page.locator('#notification-value-username').fill('e2e.user')
  await page.locator('#notification-value-issuer').fill('https://nerves.example.com/')
  await page.getByRole('dialog').getByRole('button', { name: /^发送$/ }).click()

  // The dialog only closes once the API accepted the message, and the toast is
  // short-lived, so assert on the refreshed delivery log instead.
  await expect(page.getByRole('dialog')).toHaveCount(0)
  await expect(page.locator('table')).toContainText('e2e@example.com')
})
