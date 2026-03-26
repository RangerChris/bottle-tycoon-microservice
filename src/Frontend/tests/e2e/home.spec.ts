import { test, expect } from '@playwright/test';

test('Frontpage is up', async ({ page }) => {
  await page.goto('http://localhost:3000/');
  const heading = page.getByRole('heading', { name: /Bottle Tycoon/ });
  await heading.waitFor({ timeout: 10000 });
  await expect(heading).toBeVisible();
  await expect(page.getByText('Bottles Processed')).toBeVisible();
});

test('shows truck contact error in activity log when truck service calls fail', async ({ page }) => {
  await page.route('**/initialize', async route => {
    await route.fulfill({ status: 200, body: '' });
  });

  await page.route('**/player', async route => {
    if (route.request().method() === 'GET') {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([{ id: 'player-1', credits: 1000 }])
      });
      return;
    }
    await route.fulfill({ status: 200, contentType: 'application/json', body: '{}' });
  });

  await page.route('**/player/**', async route => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: '{}' });
  });

  await page.route('**/recyclers', async route => {
    if (route.request().method() === 'GET') {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([{ id: 1, name: 'Recycler 1', capacityLevel: 0, capacity: 100 }])
      });
      return;
    }
    await route.fulfill({ status: 200, contentType: 'application/json', body: '{}' });
  });

  await page.route('**/recyclers/**', async route => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: '{}' });
  });

  await page.route('**/truck**', async route => {
    await route.abort('failed');
  });

  await page.goto('http://localhost:3000/');

  await expect(page.getByText('Failed to fetch trucks.')).toBeVisible({ timeout: 8000 });
});