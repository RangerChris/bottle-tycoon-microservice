// Tiny programmatic visitor: body + head dot. Queued visitors bounce subtly.
import { Container, Graphics } from 'pixi.js';

const BODY = 0x9ca3af;
const HEAD = 0xe5e7eb;

export function createVisitorSprite(): Container {
  const c = new Container();
  const g = new Graphics();
  g.ellipse(0, 2, 3.5, 4.5);
  g.fill({ color: BODY });
  g.circle(0, -4, 2.5);
  g.fill({ color: HEAD });
  c.addChild(g);
  return c;
}

// Queued visitors bob gently; walkers stay steady.
export function updateVisitorSprite(container: Container, queued: boolean, time: number): void {
  container.scale.y = queued ? 1 + Math.sin(time * 4 + container.position.x) * 0.08 : 1;
}