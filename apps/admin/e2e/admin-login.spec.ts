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

  await expect(page.getByText(/通知已进入投递队列/)).toBeVisible()
  await expect(page.locator('table')).toContainText('e2e@example.com')
})
