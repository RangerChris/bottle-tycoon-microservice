// Programmatic building vectors: HQ, recycling plant, recycler.
// A building's base footprint is its tile diamond (top corner at toScreen(x,y));
// walls rise from that footprint by `h`. Pure Graphics drawing.
import { Container, Graphics } from 'pixi.js';
import { toScreen, HALF_W, HALF_H } from '../../world/iso';

const ROOF = 0x10b981; // emerald
const ROOF_DARK = 0x0b815e;
const FACE_L = 0x374151;
const FACE_R = 0x1f2937;
const ACCENT = 0xfacc15; // amber details
const METAL = 0x9aa4ad;
const HALL_ROOF = 0x4b5563;

// Iso block: base diamond with half-width w centered on bottom corner (cx, cy),
// walls rising by h, roof in `roofColor`.
function drawBlock(g: Graphics, cx: number, cy: number, w: number, h: number, roofColor: number): void {
  const halfH = w / 2;
  // Roof diamond (base diamond lifted by h).
  g.moveTo(cx, cy - h - w);
  g.lineTo(cx + w, cy - h - halfH);
  g.lineTo(cx, cy - h);
  g.lineTo(cx - w, cy - h - halfH);
  g.closePath();
  g.fill({ color: roofColor });
  // Left face.
  g.moveTo(cx - w, cy - halfH);
  g.lineTo(cx, cy);
  g.lineTo(cx, cy - h);
  g.lineTo(cx - w, cy - halfH - h);
  g.closePath();
  g.fill({ color: FACE_L });
  // Right face.
  g.moveTo(cx, cy);
  g.lineTo(cx + w, cy - halfH);
  g.lineTo(cx + w, cy - halfH - h);
  g.lineTo(cx, cy - h);
  g.closePath();
  g.fill({ color: FACE_R });
}

export function drawHq(g: Graphics, sx: number, sy: number): void {
  const cx = sx;
  const cy = sy + HALF_H; // tile center-ish; block base covers the tile
  // Main block + smaller upper block + flag pole.
  drawBlock(g, cx, cy + HALF_H, HALF_W - 4, 22, ROOF_DARK);
  drawBlock(g, cx, cy + HALF_H - 22, HALF_W - 14, 12, ROOF);
  // Flag.
  g.setStrokeStyle({ width: 2, color: METAL });
  g.moveTo(cx, cy - 18);
  g.lineTo(cx, cy - 36);
  g.stroke();
  g.moveTo(cx, cy - 36);
  g.lineTo(cx + 12, cy - 32);
  g.lineTo(cx, cy - 28);
  g.closePath();
  g.fill({ color: ACCENT });
}

export function drawPlant(g: Graphics, sx: number, sy: number): void {
  const cx = sx;
  const cy = sy + HALF_H;
  // Wide conveyor hall + two silos.
  drawBlock(g, cx, cy + 6, HALF_W - 2, 18, HALL_ROOF);
  drawBlock(g, cx - HALF_W + 14, cy - 6, 9, 38, METAL);
  drawBlock(g, cx + HALF_W - 22, cy + 2, 9, 28, METAL);
  // Emerald stripe along the hall's right face.
  g.setStrokeStyle({ width: 3, color: ROOF, alpha: 0.9 });
  g.moveTo(cx - HALF_W + 8, cy - 4);
  g.lineTo(cx + HALF_W - 8, cy + 6);
  g.stroke();
}

export function drawRecycler(g: Graphics, sx: number, sy: number): void {
  const cx = sx;
  const cy = sy + HALF_H;
  // Hopper drum + chimney + emerald rim.
  drawBlock(g, cx, cy + 2, HALF_W - 12, 16, ROOF);
  g.rect(cx - 3, cy - 28, 6, 12);
  g.fill({ color: FACE_R });
  g.setStrokeStyle({ width: 2, color: ROOF_DARK, alpha: 0.8 });
  g.moveTo(cx - HALF_W + 10, cy + 1);
  g.lineTo(cx + HALF_W - 10, cy - 5);
  g.stroke();
}

// Builds a z-sorted container for one building at tile (x, y).
export function buildingContainer(kind: 'hq' | 'plant' | 'recycler', x: number, y: number): Container {
  const c = new Container();
  const s = toScreen(x, y);
  const g = new Graphics();
  if (kind === 'hq') drawHq(g, s.sx, s.sy);
  else if (kind === 'plant') drawPlant(g, s.sx, s.sy);
  else drawRecycler(g, s.sx, s.sy);
  c.addChild(g);
  c.zIndex = x + y; // painter's order down the diamond grid
  return c;
}