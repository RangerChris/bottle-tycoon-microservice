// Isometric projection for 64x32 diamonds. Pure math, no Pixi imports.
// Tile origin = its top corner; screen coords are in "world pixels" —
// the camera container handles pan/zoom on top of this.

export const TILE_W = 64;
export const TILE_H = 32;
export const HALF_W = TILE_W / 2; // 32
export const HALF_H = TILE_H / 2; // 16

export type ScreenPos = { sx: number; sy: number };

export function toScreen(x: number, y: number): ScreenPos {
  return { sx: (x - y) * HALF_W, sy: (x + y) * HALF_H };
}

// Inverse projection. Continuous corner-space coords: a tile's diamond
// interior maps to (x..x+1, y..y+1) — floor() picks the tile under any
// point, including its center (which lands exactly on the .5 boundary).
export function toTile(sx: number, sy: number): { x: number; y: number } {
  const fx = sx / HALF_W;
  const fy = sy / HALF_H;
  // sx = (x - y) * HALF_W, sy = (x + y) * HALF_H  →  solve for x, y
  const x = (fx + fy) / 2;
  const y = (fy - fx) / 2;
  return { x: Math.floor(x), y: Math.floor(y) };
}

// Camera offset that puts the world point (wx, wy) at the center of a
// viewW × viewH viewport at the given zoom. Used for startup centering and
// camera restore — both must honor zoom or the world drifts off-center.
export function cameraCenteringOn(
  viewW: number,
  viewH: number,
  wx: number,
  wy: number,
  zoom: number,
): { x: number; y: number } {
  return { x: viewW / 2 - wx * zoom, y: viewH / 2 - wy * zoom };
}