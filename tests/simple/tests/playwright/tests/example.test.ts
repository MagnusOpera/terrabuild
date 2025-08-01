import { test, expect } from '@playwright/test'

test('homepage has title', async ({ page }) => {
  await page.goto('https://playwright.dev')
  await expect(page).toHaveTitle('Fast and reliable end-to-end testing for modern web apps | Playwright')
})

