import { describe, it, expect } from 'vitest';
import { toScreen, toTile, TILE_W, TILE_H } from '../iso';

describe('iso projection', () => {
  it('maps (0,0) to origin', () => {
    const p = toScreen(0, 0);
    expect(p.sx).toBe(0);
    expect(p.sy).toBe(0);
  });

  it('maps (1,0) right-down and (0,1) left-down', () => {
    const right = toScreen(1, 0);
    const left = toScreen(0, 1);
    expect(right.sx).toBe(TILE_W / 2);
    expect(right.sy).toBe(TILE_H / 2);
    expect(left.sx).toBe(-TILE_W / 2);
    expect(left.sy).toBe(TILE_H / 2);
  });

  it('round-trips tiles through screen space', () => {
    for (let x = 0; x < 40; x += 3) {
      for (let y = 0; y < 40; y += 3) {
        const s = toScreen(x, y);
        const t = toTile(s.sx, s.sy);
        expect(t.x).toBe(x);
        expect(t.y).toBe(y);
      }
    }
  });
});