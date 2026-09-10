import { describe, it, expect } from 'vitest';
import { generateMap } from '../mapgen';
import { buildRoadGraph, bfsPath, roadStopFor, reachableRoads, roadCount } from '../roadGraph';
import { tileKey } from '../types';

const map = generateMap(42);
const graph = buildRoadGraph(map);

function key(pos: { x: number; y: number }): string {
  return tileKey(pos.x, pos.y);
}

describe('roadGraph', () => {
  it('finds a path between two road tiles', () => {
    // Grab two real road tiles from the graph.
    const keys = [...graph.keys()];
    expect(keys.length).toBeGreaterThan(0);
    const start = keys[0];
    const reached = [...reachableRoads(graph, start)];
    const target = reached[reached.length - 1] as string;
    const result = bfsPath(graph, start, target);
    expect(result.reachable).toBe(true);
    expect(result.path[0]).toBe(start);
    expect(result.path[result.path.length - 1]).toBe(target);
  });

  it('returns unreachable for a non-road destination', () => {
    const start = [...graph.keys()][0];
    const result = bfsPath(graph, start, '999,999');
    expect(result.reachable).toBe(false);
    expect(result.path).toEqual([]);
  });

  it('roadStopFor finds the adjacent road of HQ and plant', () => {
    expect(roadStopFor(map, map.hq.x, map.hq.y)).not.toBeNull();
    expect(roadStopFor(map, map.plant.x, map.plant.y)).not.toBeNull();
  });

  it('returns null stop for a tile with no adjacent road', () => {
    // Corner (0,0) is grass + not road in every generated map; its neighbors
    // could still be road, so build a tiny custom map to be sure.
    const tiny = generateMap(7);
    // Pick any non-road tile with no road neighbors by scanning.
    let found: string | null = null;
    for (let y = 0; y < tiny.height && found === null; y++) {
      for (let x = 0; x < tiny.width && found === null; x++) {
        const tile = tiny.tiles[y * tiny.width + x];
        if (tile.road || tile.buildingId) continue;
        const hasRoadNeighbor =
          (x > 0 && tiny.tiles[y * tiny.width + x - 1].road) ||
          (x < tiny.width - 1 && tiny.tiles[y * tiny.width + x + 1].road) ||
          (y > 0 && tiny.tiles[(y - 1) * tiny.width + x].road) ||
          (y < tiny.height - 1 && tiny.tiles[(y + 1) * tiny.width + x].road);
        if (!hasRoadNeighbor) found = `${x},${y}`;
      }
    }
    if (found !== null) {
      const [fx, fy] = found.split(',').map(Number);
      expect(roadStopFor(tiny, fx, fy)).toBeNull();
    }
  });

  it('reaches every road tile from the HQ stop', () => {
    const hqStop = roadStopFor(map, map.hq.x, map.hq.y) as string;
    const reached = reachableRoads(graph, hqStop);
    expect(reached.size).toBe(roadCount(map));
  });

  it('path from HQ stop to plant stop exists', () => {
    const hqStop = roadStopFor(map, map.hq.x, map.hq.y) as string;
    const plantStop = roadStopFor(map, map.plant.x, map.plant.y) as string;
    const result = bfsPath(graph, hqStop, plantStop);
    expect(result.reachable).toBe(true);
    expect(key(map.hq)).not.toBe(hqStop); // stop is the adjacent road, not the building tile
  });
});