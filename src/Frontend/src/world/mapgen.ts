// Seeded, deterministic map generation: 40x40 grid, water/forest blobs,
// a connected "ladder" road skeleton + random branches, HQ + plant placed
// on road-adjacent tiles. Invariant-checked; reseeds itself if degenerate.
import type { Tile, TileTerrain, TilePos, WorldMap, Decoration } from './types';
import { tileKey } from './types';
import { mulberry32, intBetween, pick, type Rnd } from './rng';
import { buildRoadGraph, reachableRoads, roadStopFor, roadCount, bfsPath } from './roadGraph';

export const MAP_W = 40;
export const MAP_H = 40;

const WATER_BLOBS = { min: 5, max: 8 };
const WATER_SIZE = { min: 4, max: 12 };
const FOREST_BLOBS = { min: 8, max: 12 };
const FOREST_SIZE = { min: 6, max: 16 };
const BRANCHES = { min: 6, max: 10 };
const BRANCH_LEN = { min: 3, max: 8 };
const AVENUES = 3; // horizontal rows + vertical columns
const MIN_PLANT_DISTANCE = 12;
const MIN_PLACEMENT_TILES = 10;

export function generateMap(seed: number): import('./types').WorldMap {
  let s = seed >>> 0;
  for (;;) {
    const map = tryGenerate(s);
    if (map !== null) return map;
    s = (s + 1) >>> 0; // degenerate layout → nudge the seed, try again
  }
}

function tryGenerate(seed: number): WorldMap | null {
  const rnd = mulberry32(seed);
  const tiles = emptyGrid();

  paintBlobs(rnd, tiles, 'water', intBetween(rnd, WATER_BLOBS.min, WATER_BLOBS.max), WATER_SIZE.min, WATER_SIZE.max);
  paintBlobs(rnd, tiles, 'forest', intBetween(rnd, FOREST_BLOBS.min, FOREST_BLOBS.max), FOREST_SIZE.min, FOREST_SIZE.max);
  paintRoadSkeleton(rnd, tiles);
  paintBranches(rnd, tiles);

  const map: import('./types').WorldMap = {
    seed: seed >>> 0,
    width: MAP_W,
    height: MAP_H,
    tiles,
    hq: { x: -1, y: -1 },
    plant: { x: -1, y: -1 },
  };

  const bias = { x: Math.floor(MAP_W / 2), y: Math.floor(MAP_H / 2), maxDist: 14 };
  const hqPos = pickRoadAdjacent(rnd, map, bias);
  if (!hqPos) return null;
  map.hq = hqPos;
  occupyBuilding(map, hqPos.x, hqPos.y, 'hq');

  const plantPos = plantCandidate(map, rnd);
  if (!plantPos) {
    occupyBuilding(map, hqPos.x, hqPos.y, null); // undo, degenerate
    return null;
  }
  map.plant = plantPos;
  occupyBuilding(map, plantPos.x, plantPos.y, 'plant');

  if (!invariantsHold(map)) return null;
  return map;
}

// --- terrain ---

function emptyGrid(): Tile[] {
  const tiles: Tile[] = [];
  for (let i = 0; i < MAP_W * MAP_H; i++) {
    tiles.push({ terrain: 'grass', road: false, decoration: null, buildingId: null });
  }
  return tiles;
}

function paintBlobs(rnd: Rnd, tiles: Tile[], terrain: TileTerrain, blobCount: number, minSize: number, maxSize: number): void {
  for (let b = 0; b < blobCount; b++) {
    const size = intBetween(rnd, minSize, maxSize);
    let x = intBetween(rnd, 1, MAP_W - 2);
    let y = intBetween(rnd, 1, MAP_H - 2);
    for (let step = 0; step < size; step++) {
      const tile = tiles[y * MAP_W + x];
      if (tile.terrain === 'grass' && !tile.road) {
        tile.terrain = terrain;
        // Scatter trees across forest blobs; a few clearings stay buildable.
        if (terrain === 'forest' && rnd() < 0.6) tile.decoration = 'tree';
      }
      const dir = intBetween(rnd, 0, 3);
      if (dir === 0 && x < MAP_W - 1) x++;
      else if (dir === 1 && x > 0) x--;
      else if (dir === 2 && y < MAP_H - 1) y++;
      else if (dir === 3 && y > 0) y--;
    }
  }
}

// --- roads ---

function paintRoadSkeleton(rnd: Rnd, tiles: Tile[]): void {
  const rows = jitteredLines(rnd, MAP_H);
  const cols = jitteredLines(rnd, MAP_W);
  for (const row of rows) {
    for (let x = 0; x < MAP_W; x++) setRoad(tiles, x, row);
  }
  for (const col of cols) {
    for (let y = 0; y < MAP_H; y++) setRoad(tiles, col, y);
  }
}

// 3 evenly spaced lines, each jittered ±2, kept inside bounds.
function jitteredLines(rnd: Rnd, extent: number): number[] {
  const lines: number[] = [];
  for (let i = 1; i <= AVENUES; i++) {
    const base = Math.round((extent / (AVENUES + 1)) * i);
    const jitter = intBetween(rnd, -2, 2);
    lines.push(Math.max(1, Math.min(extent - 2, base + jitter)));
  }
  return lines;
}

function setRoad(tiles: Tile[], x: number, y: number): void {
  const tile = tiles[y * MAP_W + x];
  if (tile.terrain === 'water') tile.terrain = 'grass'; // roads bridge water, keep it simple
  tile.road = true;
  tile.decoration = null;
}

function paintBranches(rnd: Rnd, tiles: Tile[]): void {
  const roadKeys: number[] = [];
  for (let i = 0; i < tiles.length; i++) {
    if (tiles[i].road) roadKeys.push(i);
  }
  const branchCount = intBetween(rnd, BRANCHES.min, BRANCHES.max);
  for (let b = 0; b < branchCount; b++) {
    const startIdx = pick(rnd, roadKeys);
    let x = startIdx % MAP_W;
    let y = Math.floor(startIdx / MAP_W);
    const len = intBetween(rnd, BRANCH_LEN.min, BRANCH_LEN.max);
    for (let step = 0; step < len; step++) {
      const dir = intBetween(rnd, 0, 3);
      if (dir === 0 && x < MAP_W - 1) x++;
      else if (dir === 1 && x > 0) x--;
      else if (dir === 2 && y < MAP_H - 1) y++;
      else if (dir === 3 && y > 0) y--;
      else continue;
      setRoad(tiles, x, y);
    }
  }
}

// --- buildings ---

// Grass tiles adjacent to a road, nearest the given bias point first (shuffled).
function pickRoadAdjacent(rnd: Rnd, map: import('./types').WorldMap, bias?: { x: number; y: number; maxDist: number }): TilePos | null {
  const candidates: TilePos[] = [];
  for (let y = 1; y < map.height - 1; y++) {
    for (let x = 1; x < map.width - 1; x++) {
      const tile = map.tiles[y * map.width + x];
      if (tile.terrain !== 'grass' || tile.road || tile.buildingId) continue;
      if (!roadAdjacent(map, x, y)) continue;
      if (bias) {
        const d = Math.abs(x - bias.x) + Math.abs(y - bias.y);
        if (d > bias.maxDist) continue;
      }
      candidates.push({ x, y });
    }
  }
  if (candidates.length === 0) return null;
  return pick(rnd, candidates);
}

function roadAdjacent(map: import('./types').WorldMap, x: number, y: number): boolean {
  const neighbors = [
    { x: x - 1, y },
    { x: x + 1, y },
    { x, y: y - 1 },
    { x, y: y + 1 },
  ];
  for (const n of neighbors) {
    if (n.x < 0 || n.y < 0 || n.x >= map.width || n.y >= map.height) continue;
    if (map.tiles[n.y * map.width + n.x].road) return true;
  }
  return false;
}

function occupyBuilding(map: import('./types').WorldMap, x: number, y: number, id: string | null): void {
  map.tiles[y * map.width + x].buildingId = id;
}

// Plant goes far from HQ (BFS road distance ≥ MIN_PLANT_DISTANCE).
function plantCandidate(map: import('./types').WorldMap, rnd: Rnd): TilePos | null {
  const graph = buildRoadGraph(map);
  const hqStop = roadStopFor(map, map.hq.x, map.hq.y);
  if (!hqStop) return null;
  const distFromHq = bfsDistance(graph, hqStop);

  const candidates: TilePos[] = [];
  for (let y = 1; y < map.height - 1; y++) {
    for (let x = 1; x < map.width - 1; x++) {
      const tile = map.tiles[y * map.width + x];
      if (tile.terrain !== 'grass' || tile.road || tile.buildingId) continue;
      const stop = roadStopFor(map, x, y);
      if (!stop) continue;
      const dist = distFromHq.get(stop);
      if (dist === undefined || dist < MIN_PLANT_DISTANCE) continue;
      candidates.push({ x, y });
    }
  }
  if (candidates.length === 0) return null;
  return pick(rnd, candidates);
}

function bfsDistance(graph: ReturnType<typeof buildRoadGraph>, start: string): Map<string, number> {
  const dist = new Map<string, number>();
  dist.set(start, 0);
  const queue: string[] = [start];
  while (queue.length > 0) {
    const current = queue.shift() as string;
    const d = dist.get(current) as number;
    for (const next of graph.get(current) ?? []) {
      if (dist.has(next)) continue;
      dist.set(next, d + 1);
      queue.push(next);
    }
  }
  return dist;
}

// --- validation ---

function invariantsHold(map: import('./types').WorldMap): boolean {
  const graph = buildRoadGraph(map);
  if (roadCount(map) === 0) return false;

  // Every road tile reachable from the HQ stop.
  const hqStop = roadStopFor(map, map.hq.x, map.hq.y);
  if (!hqStop) return false;
  const reached = reachableRoads(graph, hqStop);
  if (reached.size !== roadCount(map)) return false;

  // Plant must also be reachable (it is, if all roads are — but check the stop).
  const plantStop = roadStopFor(map, map.plant.x, map.plant.y);
  if (!plantStop || !reached.has(plantStop)) return false;

  // Enough room for the player to build.
  let placeable = 0;
  for (let y = 0; y < map.height; y++) {
    for (let x = 0; x < map.width; x++) {
      const tile = map.tiles[y * map.width + x];
      if (tile.terrain !== 'grass' || tile.road || tile.buildingId || tile.decoration) continue;
      if (roadAdjacent(map, x, y)) placeable++;
    }
  }
  return placeable >= MIN_PLACEMENT_TILES;
}

export { intBetween, mulberry32 }; // re-export for tests convenience