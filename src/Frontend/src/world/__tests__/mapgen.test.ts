import { describe, it, expect } from 'vitest';
import { generateMap, MAP_W, MAP_H } from '../mapgen';
import { buildRoadGraph, reachableRoads, roadStopFor, roadCount, bfsPath } from '../roadGraph';
import { toTile, toScreen } from '../iso';

describe('generateMap', () => {
  it('is deterministic for the same seed', () => {
    const a = generateMap(12345);
    const b = generateMap(12345);
    expect(a.tiles).toEqual(b.tiles);
    expect(a.hq).toEqual(b.hq);
    expect(a.plant).toEqual(b.plant);
  });

  it('has correct dimensions', () => {
    const map = generateMap(1);
    expect(map.width).toBe(MAP_W);
    expect(map.height).toBe(MAP_H);
    expect(map.tiles.length).toBe(MAP_W * MAP_H);
  });

  it('places HQ and plant on road-adjacent grass tiles', () => {
    for (const seed of [1, 42, 999]) {
      const map = generateMap(seed);
      expect(roadStopFor(map, map.hq.x, map.hq.y)).not.toBeNull();
      expect(roadStopFor(map, map.plant.x, map.plant.y)).not.toBeNull();
      const hqTile = map.tiles[map.hq.y * map.width + map.hq.x];
      const plantTile = map.tiles[map.plant.y * map.width + map.plant.x];
      expect(hqTile.road).toBe(false);
      expect(plantTile.road).toBe(false);
      expect(hqTile.terrain).toBe('grass');
      expect(plantTile.terrain).toBe('grass');
      expect(hqTile.buildingId).toBe('hq');
      expect(plantTile.buildingId).toBe('plant');
    }
  });

  it('has a fully connected road network from the HQ stop', () => {
    for (const seed of [1, 42, 999, 20250910]) {
      const map = generateMap(seed);
      const graph = buildRoadGraph(map);
      const hqStop = roadStopFor(map, map.hq.x, map.hq.y) as string;
      const reached = reachableRoads(graph, hqStop);
      expect(reached.size).toBe(roadCount(map));
    }
  });

  it('guarantees at least 10 valid placement tiles', () => {
    for (const seed of [1, 42, 999, 20250910]) {
      const map = generateMap(seed);
      let placeable = 0;
      for (const tile of map.tiles) {
        if (tile.terrain === 'grass' && !tile.road && !tile.buildingId && !tile.decoration) placeable++;
      }
      expect(placeable).toBeGreaterThanOrEqual(10);
    }
  });

  it('plants the recycler plant far from HQ by road distance', () => {
    const map = generateMap(42);
    const graph = buildRoadGraph(map);
    const hqStop = roadStopFor(map, map.hq.x, map.hq.y) as string;
    const plantStop = roadStopFor(map, map.plant.x, map.plant.y) as string;
    const result = bfsPath(graph, hqStop, plantStop);
    expect(result.reachable).toBe(true);
    expect(result.path.length).toBeGreaterThanOrEqual(12);
  });

  it('round-trips tile coords through iso projection', () => {
    const map = generateMap(42);
    const some = map.tiles[5 * map.width + 7];
    expect(some).toBeDefined();
    const s = toScreen(7, 5);
    const t = toTile(s.sx, s.sy);
    expect(t.x).toBe(7);
    expect(t.y).toBe(5);
  });
});