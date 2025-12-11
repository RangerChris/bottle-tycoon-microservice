import { test, expect } from '@playwright/test';

test('Frontpage is up', async ({ page }) => {
  await page.goto('http://localhost:3000/');
  const heading = page.getByRole('heading', { name: /Bottle Tycoon/ });
  await heading.waitFor({ timeout: 10000 });
  await expect(heading).toBeVisible();
  await expect(page.getByText('Bottles Processed')).toBeVisible();
});