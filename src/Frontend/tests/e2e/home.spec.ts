import { test, expect } from '@playwright/test';

test('home page shows health', async ({ page }) => {
  await page.route('**/health', route => route.fulfill({ status: 200, body: 'OK' }));
  await page.goto('/');
  await expect(page.locator('text=API Gateway Health')).toBeVisible();
  await expect(page.locator('text=OK')).toBeVisible();
});