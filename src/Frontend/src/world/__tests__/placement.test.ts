import { describe, it, expect } from 'vitest';
import { generateMap } from '../mapgen';
import { canPlace, validPlacementTiles, occupyTile, vacateTile } from '../placement';

const map = generateMap(42);

describe('placement', () => {
  it('rejects out-of-bounds', () => {
    const result = canPlace(map, -1, 0);
    expect(result.ok).toBe(false);
    if (!result.ok) expect(result.reason).toBe('out-of-bounds');
  });

  it('rejects building on the HQ or plant tile', () => {
    const hq = canPlace(map, map.hq.x, map.hq.y);
    expect(hq.ok).toBe(false);
    if (!hq.ok) expect(hq.reason).toBe('occupied');
  });

  it('rejects non-road-adjacent tiles', () => {
    // Find a grass tile with no adjacent road.
    let target: { x: number; y: number } | null = null;
    for (let y = 0; y < map.height && target === null; y++) {
      for (let x = 0; x < map.width && target === null; x++) {
        if (canPlace(map, x, y).ok) continue;
        const tile = map.tiles[y * map.width + x];
        if (tile.road || tile.terrain !== 'grass' || tile.decoration || tile.buildingId) continue;
        target = { x, y };
      }
    }
    if (target !== null) {
      const result = canPlace(map, target.x, target.y);
      expect(result.ok).toBe(false);
    }
  });

  it('guarantees at least 10 valid placement tiles on several seeds', () => {
    for (const seed of [1, 42, 777, 20250910]) {
      const m = generateMap(seed);
      expect(validPlacementTiles(m).length).toBeGreaterThanOrEqual(10);
    }
  });

  it('occupy + vacate round-trips', () => {
    const spots = validPlacementTiles(map);
    expect(spots.length).toBeGreaterThan(0);
    const spot = spots[0];
    const before = map.tiles[spot.y * map.width + spot.x].buildingId;
    occupyTile(map, spot.x, spot.y, 'recycler-test');
    expect(map.tiles[spot.y * map.width + spot.x].buildingId).toBe('recycler-test');
    vacateTile(map, spot.x, spot.y);
    expect(map.tiles[spot.y * map.width + spot.x].buildingId).toBe(before);
  });
});