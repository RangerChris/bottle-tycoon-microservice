// Pure data model for the isometric world. No framework or Pixi imports here —
// this layer is unit-testable with Vitest in node.

export type TileTerrain = 'grass' | 'water' | 'forest';

export type Decoration = 'tree' | 'rock' | null;

export type BuildingKind = 'hq' | 'plant' | 'recycler';

export type Tile = {
  terrain: TileTerrain;
  road: boolean;
  decoration: Decoration;
  buildingId: string | null;
};

export type TilePos = { x: number; y: number };

export type WorldBuilding = {
  id: string; // 'hq' | 'plant' | `recycler-${entityId}`
  kind: BuildingKind;
  x: number;
  y: number;
  entityId?: number | string; // RecyclerService entity id (recyclers only)
};

export type WorldMap = {
  seed: number;
  width: number;
  height: number;
  tiles: Tile[]; // row-major, index = y * width + x
  hq: TilePos;
  plant: TilePos;
};

export type TruckPhase = 'atHq' | 'toRecycler' | 'loading' | 'toPlant' | 'unloading' | 'toHq';

export type TruckVisual = {
  truckId: number | string;
  path: string[]; // road tile keys "x,y"
  legIndex: number;
  legProgress: number; // 0..1 within current segment
  phase: TruckPhase;
};

export type BuildMode = 'none' | 'recycler';

// "x,y" tile key helpers — used across map, pathfinding and persistence.
export function tileKey(x: number, y: number): string {
  return `${x},${y}`;
}

export function parseTileKey(key: string): TilePos {
  const idx = key.indexOf(',');
  return { x: Number(key.slice(0, idx)), y: Number(key.slice(idx + 1)) };
}