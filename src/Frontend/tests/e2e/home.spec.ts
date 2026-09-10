import { test, expect } from '@playwright/test';

test('Frontpage is up', async ({ page }) => {
  await page.goto('http://localhost:3000/');
  const heading = page.getByRole('heading', { name: /Bottle Tycoon/ });
  await heading.waitFor({ timeout: 10000 });
  await expect(heading).toBeVisible();
  // Title appears in both the HUD card and the classic panels drawer.
  await expect(page.getByText('Bottles Processed').first()).toBeVisible();
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

  // Anchored to the truck service path: `**/truck**` would also match
  // /src/world/truckSim.ts and break module loading.
  await page.route('**/truck', async route => {
    await route.abort('failed');
  });
  await page.route('**/truck/**', async route => {
    await route.abort('failed');
  });

  await page.goto('http://localhost:3000/');

  // The log renders in both the HUD card and the classic panels drawer.
  await expect(page.getByText('Failed to fetch trucks.').first()).toBeVisible({ timeout: 8000 });
});