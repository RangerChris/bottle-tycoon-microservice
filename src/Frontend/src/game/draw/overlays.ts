// Overlay drawing: placement ghost (valid/invalid tint) + selection ring.
// Redrawn only when the relevant world state changes (engine owns when).
import { Container, Graphics } from 'pixi.js';
import type { WorldMap, BuildMode, TilePos } from '../../world/types';
import { canPlace } from '../../world/placement';
import { toScreen, HALF_W, TILE_H } from '../../world/iso';

const VALID = 0x10b981;
const INVALID = 0xef4444;
const SELECTION = 0xfacc15;

// Standard tile diamond, grown outward by `grow` px about its center.
function diamondPath(g: Graphics, sx: number, sy: number, grow = 0): void {
  g.moveTo(sx, sy - grow);
  g.lineTo(sx + HALF_W + grow / 2, sy + TILE_H / 2);
  g.lineTo(sx, sy + TILE_H + grow);
  g.lineTo(sx - HALF_W - grow / 2, sy + TILE_H / 2);
  g.closePath();
}

export type OverlayState = {
  map: WorldMap;
  buildMode: BuildMode;
  hoveredTile: TilePos | null;
  selectedBuilding: { id: string; x: number; y: number } | null;
};

export function updateOverlays(layers: { overlays: Container }, state: OverlayState): void {
  layers.overlays.removeChildren();

  if (state.buildMode === 'recycler' && state.hoveredTile) {
    const { x, y } = state.hoveredTile;
    if (x >= 0 && y >= 0 && x < state.map.width && y < state.map.height) {
      const s = toScreen(x, y);
      const g = new Graphics();
      const verdict = canPlace(state.map, x, y);
      const color = verdict.ok ? VALID : INVALID;
      diamondPath(g, s.sx, s.sy);
      g.fill({ color, alpha: 0.3 });
      diamondPath(g, s.sx, s.sy);
      g.setStrokeStyle({ width: 2, color, alpha: 0.9 });
      g.stroke();
      layers.overlays.addChild(g);
    }
  }

  if (state.selectedBuilding) {
    const s = toScreen(state.selectedBuilding.x, state.selectedBuilding.y);
    const g = new Graphics();
    diamondPath(g, s.sx, s.sy, 8);
    g.setStrokeStyle({ width: 2.5, color: SELECTION, alpha: 0.95 });
    g.stroke();
    layers.overlays.addChild(g);
  }
}