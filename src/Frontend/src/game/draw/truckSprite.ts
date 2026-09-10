// Programmatic truck sprite: trailer + cab drawn pointing +x, then rotated
// to the iso direction of travel. The cab is tinted per phase; the trailer
// darkens when loaded. Children are tinted, never redrawn per frame.
import { Container, Graphics } from 'pixi.js';
import type { TruckPhase } from '../../world/types';

export const PHASE_COLORS: Record<TruckPhase, number> = {
  atHq: 0x9ca3af,
  toRecycler: 0x10b981,
  loading: 0xfacc15,
  toPlant: 0xfacc15,
  unloading: 0xfacc15,
  toHq: 0x9ca3af,
};

const WHEELS = 0x111827;

export function createTruckSprite(): Container {
  const c = new Container();
  // Truck points +x: trailer behind, cab in front.
  const trailer = new Graphics();
  trailer.roundRect(-20, -6, 26, 12, 2);
  trailer.fill({ color: 0xffffff });
  trailer.tint = TRAILER_IDLE;
  const cab = new Graphics();
  cab.roundRect(6, -5, 10, 10, 2);
  cab.fill({ color: 0xffffff });
  cab.tint = PHASE_COLORS.atHq;
  const wheels = new Graphics();
  wheels.circle(-14, 6, 2.5);
  wheels.circle(-4, 6, 2.5);
  wheels.circle(10, 6, 2.5);
  wheels.fill({ color: WHEELS });
  c.addChild(trailer, wheels, cab);
  return c;
}

const TRAILER_IDLE = 0x374151;
const TRAILER_LOADED = 0x6b5620;

// Repositions/orients/re-colors an existing sprite in place (no redraws).
export function updateTruckSprite(
  container: Container,
  phase: TruckPhase,
  dirX: number,
  dirY: number,
  loaded: boolean,
): void {
  // Iso screen delta for a tile-step (dirX, dirY).
  const dx = dirX - dirY;
  const dy = dirX + dirY;
  const rotation = Math.atan2(dy * 16, dx * 32);
  container.rotation = rotation;
  // Keep the cab from rendering upside-down on left/up travel.
  const normalized = ((rotation % (Math.PI * 2)) + Math.PI * 2) % (Math.PI * 2);
  container.scale.y = normalized > Math.PI / 2 && normalized < (3 * Math.PI) / 2 ? -1 : 1;

  const [trailer, , cab] = container.children as Graphics[];
  cab.tint = PHASE_COLORS[phase];
  trailer.tint = loaded ? TRAILER_LOADED : TRAILER_IDLE;
}