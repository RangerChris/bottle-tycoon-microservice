import { test, expect, type Page } from '@playwright/test';

// Backend-free world e2e: mocks the five services the same way home.spec.ts
// does, then drives the isometric world through its debug hook (__world)
// and the real DOM (canvas clicks, toolbar buttons).

type Spot = { x: number; y: number };

async function findValidSpot(target: Page): Promise<Spot> {
  return target.evaluate(() => {
    const m = (window as any).__world.map as {
      width: number; height: number; hq: { x: number; y: number };
      tiles: Array<{ terrain: string; road: boolean; buildingId: string | null; decoration: string | null }>;
    };
    const key = (x: number, y: number) => y * m.width + x;
    const ok = (x: number, y: number) => {
      if (x < 0 || y < 0 || x >= m.width || y >= m.height) return false;
      const t = m.tiles[key(x, y)];
      if (t.terrain === 'water' || t.road || t.buildingId || t.decoration) return false;
      return [[1, 0], [-1, 0], [0, 1], [0, -1]].some(([dx, dy]) => {
        const nx = x + dx, ny = y + dy;
        return nx >= 0 && ny >= 0 && nx < m.width && ny < m.height && m.tiles[key(nx, ny)].road;
      });
    };
    for (let r = 1; r < 10; r++) {
      for (let dy = -r; dy <= r; dy++) {
        for (let dx = -r; dx <= r; dx++) {
          if (ok(m.hq.x + dx, m.hq.y + dy)) return { x: m.hq.x + dx, y: m.hq.y + dy };
        }
      }
    }
    throw new Error('no valid placement spot near HQ');
  });
}

// Tile → page coordinates, accounting for camera pan/zoom and canvas offset.
async function clickTile(tile: Spot, target: Page) {
  const cam = await target.evaluate(() => (window as any).__world.camera);
  const box = await target.locator('[data-testid="game-canvas"]').boundingBox();
  if (!box) throw new Error('canvas not found');
  const sx = (tile.x - tile.y) * 32;
  const sy = (tile.x + tile.y) * 16;
  await target.mouse.click(box.x + cam.x + sx * cam.zoom, box.y + cam.y + sy * cam.zoom);
}

// Minimal service mocks: a funded player, empty world, echo-on-create.
async function mockServices(target: Page) {
  await target.route('**/initialize', route => route.fulfill({ status: 200, body: '' }));
  await target.route('**/player', route => {
    if (route.request().method() === 'GET') {
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([{ id: 'player-1', credits: 2000 }]),
      });
    }
    return route.fulfill({ status: 200, contentType: 'application/json', body: '{}' });
  });
  await target.route('**/player/**', route => route.fulfill({ status: 200, contentType: 'application/json', body: '{}' }));
  await target.route('**/recyclers', route => {
    if (route.request().method() === 'GET') {
      return route.fulfill({ status: 200, contentType: 'application/json', body: '[]' });
    }
    const body = route.request().postDataJSON() as { location?: string };
    return route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ id: 'r-1', capacityLevel: 0, capacity: 100, location: body.location ?? null }),
    });
  });
  await target.route('**/recyclers/**', route => route.fulfill({ status: 200, contentType: 'application/json', body: '{}' }));
  // NOTE: glob must be anchored to the /truck path — `**/truck**` would also
  // match /src/world/truckSim.ts and break module loading.
  await target.route('**/truck', route => {
    if (route.request().method() === 'GET') {
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([{ id: 't-1', level: 0, capacity: 45, currentLoad: 0, status: 'idle' }]),
      });
    }
    return route.fulfill({ status: 200, contentType: 'application/json', body: '{}' });
  });
  await target.route('**/truck/**', route => route.fulfill({ status: 200, contentType: 'application/json', body: '{}' }));
}

test.describe('isometric world', () => {
  test.beforeEach(async ({ page }) => {
    await mockServices(page);
    await page.goto('http://localhost:3000/');
    await expect(page.locator('[data-testid="game-canvas"] canvas').first()).toBeVisible({ timeout: 15000 });
    await page.waitForFunction(() => (window as any).__world?.map != null);
  });

  test('renders a generated world with road-adjacent HQ and plant', async ({ page }) => {
    const map = await page.evaluate(() => {
      const m = (window as any).__world.map;
      const key = (x: number, y: number) => y * m.width + x;
      const adjacentRoad = (b: { x: number; y: number }) =>
        [[1, 0], [-1, 0], [0, 1], [0, -1]].some(([dx, dy]) => m.tiles[key(b.x + dx, b.y + dy)].road);
      return {
        width: m.width,
        height: m.height,
        hq: m.hq,
        plant: m.plant,
        hqRoadAdjacent: adjacentRoad(m.hq),
        plantRoadAdjacent: adjacentRoad(m.plant),
        roadCount: m.tiles.filter((t: any) => t.road).length,
      };
    });
    expect(map.width).toBe(40);
    expect(map.height).toBe(40);
    expect(map.hqRoadAdjacent).toBe(true);
    expect(map.plantRoadAdjacent).toBe(true);
    expect(map.roadCount).toBeGreaterThan(20);
    expect(map.hq).not.toEqual(map.plant);
  });

  test('buy + place a recycler on a road-adjacent tile', async ({ page }) => {
    const spot = await findValidSpot(page);

    await page.getByRole('button', { name: /\+ Buy Recycler/ }).click();
    await expect(page.getByRole('button', { name: /Click a tile next to a road/ })).toBeVisible();
    await expect(page.getByText('Esc to cancel')).toBeVisible();

    await clickTile(spot, page);

    // Building lands on the map and the economy state carries its location.
    await page.waitForFunction(() => {
      const w = (window as any).__world;
      return Object.values(w.buildings).some((b: any) => b.kind === 'recycler');
    });
    const building = await page.evaluate(() =>
      Object.values((window as any).__world.buildings).find((b: any) => b.kind === 'recycler'),
    );
    expect(building).toMatchObject({ x: spot.x, y: spot.y });

    // 2000 − 500 placement cost.
    await expect(page.getByText(/Credits\s*1,?500/)).toBeVisible();
  });

  test('Esc cancels placement mode without buying', async ({ page }) => {
    await page.getByRole('button', { name: /\+ Buy Recycler/ }).click();
    await expect(page.getByRole('button', { name: /Click a tile next to a road/ })).toBeVisible();
    await page.keyboard.press('Escape');
    await expect(page.getByRole('button', { name: /\+ Buy Recycler/ })).toBeVisible({ timeout: 5000 });

    // No purchase happened: credits unchanged, no recycler building.
    await expect(page.getByText(/Credits\s*2,?000/)).toBeVisible();
    const recyclers = await page.evaluate(() =>
      Object.values((window as any).__world.buildings).filter((b: any) => b.kind === 'recycler').length,
    );
    expect(recyclers).toBe(0);
  });
});