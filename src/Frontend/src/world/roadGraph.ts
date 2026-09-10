// Road graph over road tiles: 4-neighbor adjacency + BFS pathfinding.
// Grid roads are unweighted, so BFS (not A*) — simpler, deterministic.
import type { TilePos, WorldMap } from './types';
import { tileKey } from './types';

export type RoadGraph = Map<string, string[]>; // tileKey → neighbor keys

// Builds the adjacency map once per map; recycler placement only ever ADDS
// roads? No — roads come from mapgen only. Buildings never block roads.
export function buildRoadGraph(map: WorldMap): RoadGraph {
  const graph = new Map<string, string[]>();
  for (let y = 0; y < map.height; y++) {
    for (let x = 0; x < map.width; x++) {
      const tile = map.tiles[y * map.width + x];
      if (!tile.road) continue;
      const neighbors: string[] = [];
      if (x > 0 && map.tiles[y * map.width + x - 1].road) neighbors.push(tileKey(x - 1, y));
      if (x < map.width - 1 && map.tiles[y * map.width + x + 1].road) neighbors.push(tileKey(x + 1, y));
      if (y > 0 && map.tiles[(y - 1) * map.width + x].road) neighbors.push(tileKey(x, y - 1));
      if (y < map.height - 1 && map.tiles[(y + 1) * map.width + x].road) neighbors.push(tileKey(x, y + 1));
      graph.set(tileKey(x, y), neighbors);
    }
  }
  return graph;
}

export type PathResult = { path: string[]; reachable: boolean };

// BFS shortest path between two road tile keys. Empty path + reachable=false
// when there is no route.
export function bfsPath(graph: RoadGraph, from: string, to: string): PathResult {
  if (from === to) return { path: [from], reachable: true };
  if (!graph.has(from) || !graph.has(to)) return { path: [], reachable: false };

  const cameFrom = new Map<string, string>();
  const queue: string[] = [from];
  cameFrom.set(from, from);

  while (queue.length > 0) {
    const current = queue.shift() as string;
    const neighbors = graph.get(current) ?? [];
    for (const next of neighbors) {
      if (cameFrom.has(next)) continue;
      cameFrom.set(next, current);
      if (next === to) {
        return { path: reconstruct(cameFrom, from, to), reachable: true };
      }
      queue.push(next);
    }
  }
  return { path: [], reachable: false };
}

function reconstruct(cameFrom: Map<string, string>, from: string, to: string): string[] {
  const path: string[] = [];
  let current = to;
  while (current !== from) {
    path.push(current);
    current = cameFrom.get(current) as string;
  }
  path.push(from);
  path.reverse();
  return path;
}

// Nearest road tile adjacent to a building tile (the truck "stop").
export function roadStopFor(map: WorldMap, x: number, y: number): string | null {
  const candidates = [
    { x: x - 1, y },
    { x: x + 1, y },
    { x, y: y - 1 },
    { x, y: y + 1 },
  ];
  for (const c of candidates) {
    if (c.x < 0 || c.y < 0 || c.x >= map.width || c.y >= map.height) continue;
    if (map.tiles[c.y * map.width + c.x].road) return tileKey(c.x, c.y);
  }
  return null;
}

// Every road tile reachable from a start key (used by mapgen validation).
export function reachableRoads(graph: RoadGraph, start: string): Set<string> {
  const seen = new Set<string>();
  if (!graph.has(start)) return seen;
  const queue: string[] = [start];
  seen.add(start);
  while (queue.length > 0) {
    const current = queue.shift() as string;
    for (const next of graph.get(current) ?? []) {
      if (seen.has(next)) continue;
      seen.add(next);
      queue.push(next);
    }
  }
  return seen;
}

export function roadCount(map: WorldMap): number {
  let count = 0;
  for (const tile of map.tiles) {
    if (tile.road) count++;
  }
  return count;
}

export type { TilePos };