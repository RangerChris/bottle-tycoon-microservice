// In-world capacity bars above recyclers (and HQ/plant label glow later).
// Redrawn only on store changes, never per-frame.
import { Container, Graphics } from 'pixi.js';
import { toScreen, HALF_W } from '../../world/iso';

const BG = 0x111827;
const FILL_LOW = 0x10b981; // emerald
const FILL_HIGH = 0xfacc15; // amber when ≥80% (dispatch imminent)
const BORDER = 0x374151;

const BAR_W = 44;
const BAR_H = 7;

export type StatusBarInput = { key: string; x: number; y: number; fill: number };

// Reconciles bar containers into the given layer; `fill` is 0..1.
export function updateStatusBars(layer: Container, items: StatusBarInput[]): void {
  const wanted = new Set(items.map((i) => i.key));
  for (const c of [...layer.children]) {
    const label = (c as unknown as { label?: string }).label;
    if (typeof label === 'string' && label.startsWith('bar-') && !wanted.has(label.slice(4))) {
      c.destroy({ children: true });
    }
  }
  for (const item of items) {
    let bar = layer.children.find((c) => (c as unknown as { label?: string }).label === `bar-${item.key}`);
    if (!bar) {
      bar = new Container();
      (bar as unknown as { label: string }).label = `bar-${item.key}`;
      const bg = new Graphics();
      bg.rect(-BAR_W / 2 - 1, -1, BAR_W + 2, BAR_H + 2);
      bg.fill({ color: BG, alpha: 0.85 });
      bg.setStrokeStyle({ width: 1, color: BORDER });
      bg.stroke();
      bar.addChild(bg);
      layer.addChild(bar);
    }
    const s = toScreen(item.x, item.y);
    bar.position.set(s.sx + HALF_W / 2, s.sy - 12);
    // Fill bar: remove + redraw the fill Graphics only when the ratio changes.
    const fillG = bar.children.find((c) => (c as unknown as { label?: string }).label === 'fill') as Graphics | undefined;
    const ratio = Math.max(0, Math.min(1, item.fill));
    const lastRatio = (bar as unknown as { __ratio?: number }).__ratio;
    if (fillG && lastRatio !== undefined && Math.abs(lastRatio - ratio) < 0.005) continue;
    (bar as unknown as { __ratio?: number }).__ratio = ratio;
    if (fillG) fillG.destroy();
    const g = new Graphics();
    const w = Math.max(0, (BAR_W - 2) * ratio);
    if (w > 0) {
      g.rect(-BAR_W / 2 + 1, 0, w, BAR_H);
      g.fill({ color: ratio >= 0.8 ? FILL_HIGH : FILL_LOW });
    }
    (g as unknown as { label: string }).label = 'fill';
    bar.addChild(g);
  }
}