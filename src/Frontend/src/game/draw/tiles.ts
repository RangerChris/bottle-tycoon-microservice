// Programmatic terrain + road drawing on Pixi Graphics (64x32 iso diamonds).
import { Container, Graphics } from 'pixi.js';
import type { WorldMap, Tile } from '../../world/types';
import { toScreen } from '../../world/iso';
import { TILE_W, TILE_H, HALF_W, HALF_H } from '../../world/iso';

// Palette (matches the emerald/dark app theme).
const GRASS_A = 0x1a4034;
const GRASS_B = 0x1d4839;
const WATER_A = 0x144a58;
const WATER_B = 0x174f5e;
const FOREST_GROUND = 0x17382b;
const TREE_GREEN = 0x0f4d33;
const TREE_DARK = 0x0c3f2a;
const TRUNK = 0x5b4636;
const ROAD_FILL = 0x2b2f33;
const ROAD_EDGE = 0x454c52;
const ROAD_LINE = 0x9aa4ad;

export type TileLayers = {
  terrain: Container;
  roads: Container;
};

// Hash of tile coords → stable 0/1 variant so grass has a subtle two-tone.
function tileVariant(x: number, y: number): number {
  const h = Math.imul(x + 0x9e37, y + 0x85eb) >>> 0;
  return h % 2;
}

function drawDiamond(g: Graphics, sx: number, sy: number, color: number, edge?: number): void {
  g.moveTo(sx, sy);
  g.lineTo(sx + HALF_W, sy + HALF_H);
  g.lineTo(sx, sy + TILE_H);
  g.lineTo(sx - HALF_W, sy + HALF_H);
  g.closePath();
  g.fill({ color });
  if (edge !== undefined) {
    g.setStrokeStyle({ width: 1, color: edge, alpha: 0.35 });
    g.stroke();
  }
}

function drawTree(g: Graphics, sx: number, sy: number, kind: number): void {
  // Trunk: small vertical line from diamond center.
  const cx = sx;
  const cy = sy + HALF_H;
  g.rect(cx - 1.5, cy - 8, 3, 8);
  g.fill({ color: TRUNK });
  // Canopy: 1-2 stacked triangles, slight per-tile variation.
  const c1 = kind === 0 ? TREE_GREEN : TREE_DARK;
  g.moveTo(cx, cy - 26);
  g.lineTo(cx + 8, cy - 8);
  g.lineTo(cx - 8, cy - 26);
  g.closePath();
  g.fill({ color: c1 });
  g.moveTo(cx, cy - 20);
  g.lineTo(cx + 10, cy - 4);
  g.lineTo(cx - 10, cy - 4);
  g.closePath();
  g.fill({ color: c1 });
}

function drawRoad(g: Graphics, sx: number, sy: number): void {
  g.moveTo(sx, sy);
  g.lineTo(sx + HALF_W, sy + HALF_H);
  g.lineTo(sx, sy + TILE_H);
  g.lineTo(sx - HALF_W, sy + HALF_H);
  g.closePath();
  g.fill({ color: ROAD_FILL });
  g.setStrokeStyle({ width: 1, color: ROAD_EDGE, alpha: 0.6 });
  g.stroke();
  // Dashed center line: short strokes along the diamond's horizontal midline.
  g.setStrokeStyle({ width: 2, color: ROAD_LINE, alpha: 0.5 });
  for (const off of [-18, -6, 6, 18]) {
    g.moveTo(sx + off, sy + HALF_H + off / 2);
    g.lineTo(sx + off + 7, sy + HALF_H + off / 2 + 3.5);
  }
  g.stroke();
}

// Draws the whole static map once into the given containers.
export function drawWorldTiles(map: WorldMap, layers: TileLayers): void {
  layers.terrain.removeChildren();
  layers.roads.removeChildren();
  for (let y = 0; y < map.height; y++) {
    for (let x = 0; x < map.width; x++) {
      const tile = map.tiles[y * map.width + x];
      const s = toScreen(x, y);
      const g = new Graphics();
      if (tile.road) {
        drawRoad(g, s.sx, s.sy);
        layers.roads.addChild(g);
        continue;
      }
      let color = tileVariant(x, y) === 0 ? GRASS_A : GRASS_B;
      if (tile.terrain === 'water') {
        color = tileVariant(x, y) === 0 ? WATER_A : WATER_B;
      } else if (tile.terrain === 'forest') {
        color = FOREST_GROUND;
      }
      g.moveTo(s.sx, s.sy);
      g.lineTo(s.sx + HALF_W, s.sy + HALF_H);
      g.lineTo(s.sx, s.sy + TILE_H);
      g.lineTo(s.sx - HALF_W, s.sy + HALF_H);
      g.closePath();
      g.fill({ color });
      if (tile.terrain === 'grass') {
        g.setStrokeStyle({ width: 1, color: 0x0f2c24, alpha: 0.25 });
        g.stroke();
      }
      layers.terrain.addChild(g);
      if (tile.decoration === 'tree') {
        layers.terrain.addChild(withTree(s.sx, s.sy, tileVariant(x, y)));
      } else if (tile.decoration === 'rock') {
        layers.terrain.addChild(withRock(s.sx, s.sy));
      }
    }
  }
}

function withTree(sx: number, sy: number, kind: number): Container {
  const c = new Container();
  const g = new Graphics();
  drawTree(g, sx, sy, kind);
  c.addChild(g);
  return c;
}

function withRock(sx: number, sy: number): Container {
  const c = new Container();
  const g = new Graphics();
  const cx = sx;
  const cy = sy + HALF_H;
  g.moveTo(cx - 7, cy + 2);
  g.lineTo(cx - 3, cy - 6);
  g.lineTo(cx + 5, cy - 4);
  g.lineTo(cx + 7, cy + 2);
  g.closePath();
  g.fill({ color: 0x565d63 });
  c.addChild(g);
  return c;
}