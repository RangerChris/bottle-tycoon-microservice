// Placement validation: buildings must sit on a buildable, unoccupied tile
// that is 4-neighbor adjacent to a road, so trucks can always reach them.
import type { WorldMap, TilePos } from './types';

export type PlacementError = 'water' | 'occupied' | 'blocked' | 'no-road' | 'out-of-bounds';

export type PlacementResult = { ok: true } | { ok: false; reason: PlacementError };

// Decorations (trees/rocks) are cosmetic but must be cleared before building —
// treat 'tree' as blocking, 'rock' as buildable-around (kept simple: both block).
export function canPlace(map: WorldMap, x: number, y: number): PlacementResult {
  if (x < 0 || y < 0 || x >= map.width || y >= map.height) {
    return { ok: false, reason: 'out-of-bounds' };
  }
  const tile = map.tiles[y * map.width + x];
  if (tile.terrain === 'water') return { ok: false, reason: 'water' };
  if (tile.buildingId) return { ok: false, reason: 'occupied' };
  if (tile.decoration) return { ok: false, reason: 'blocked' };
  if (!hasAdjacentRoad(map, x, y)) return { ok: false, reason: 'no-road' };
  return { ok: true };
}

export function hasAdjacentRoad(map: WorldMap, x: number, y: number): boolean {
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

// All tiles the player is allowed to build a recycler on right now.
export function validPlacementTiles(map: WorldMap): TilePos[] {
  const out: TilePos[] = [];
  for (let y = 0; y < map.height; y++) {
    for (let x = 0; x < map.width; x++) {
      const result = canPlace(map, x, y);
      if (result.ok) out.push({ x, y });
    }
  }
  return out;
}

// Marks a building onto the map (mutates the tile's buildingId). Caller owns
// the map instance — world store owns the single live map.
export function occupyTile(map: WorldMap, x: number, y: number, buildingId: string): void {
  map.tiles[y * map.width + x].buildingId = buildingId;
}

export function vacateTile(map: WorldMap, x: number, y: number): void {
  map.tiles[y * map.width + x].buildingId = null;
}