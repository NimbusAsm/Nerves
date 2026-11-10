import { expect, test } from '@playwright/test'

const username = process.env.ADMIN_USERNAME ?? 'admin'
const password = process.env.ADMIN_PASSWORD ?? 'ChangeMe!123'

test('signs in through the IdP and reaches the console', async ({ page }) => {
  await page.goto('/')

  // The SPA redirects anonymous visitors to its login route.
  await expect(page.locator('#login-username')).toBeVisible()
  await page.locator('#login-username').fill(username)
  await page.locator('#login-password').fill(password)
  await page.getByRole('button', { name: /登录|sign in/i }).click()

  // Login goes: API ticket -> IdP authorize -> /oidc-callback.html -> console.
  await page.waitForURL((url) => !url.pathname.startsWith('/login'), { timeout: 30_000 })
  await expect(page.locator('#login-username')).toHaveCount(0)
  await expect(page.getByRole('navigation').first()).toBeVisible()

  // The access token is held in memory only.
  const stored = await page.evaluate(() => Object.keys(localStorage).filter((key) => key.includes('token')))
  expect(stored).toEqual([])
})
