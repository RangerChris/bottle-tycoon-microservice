// Thin React wrapper: mounts the Pixi WorldEngine into a div and tears it
// down on unmount. Never subscribes to per-frame world state.
import { useEffect, useRef } from 'react';
import * as WorldEngine from '../../game/WorldEngine';

export default function GameCanvas() {
  const containerRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const el = containerRef.current;
    if (!el) return;
    void WorldEngine.init(el);
    return () => WorldEngine.destroy();
  }, []);

  return (
    <div
      ref={containerRef}
      className="absolute inset-0 overflow-hidden rounded-xl border border-gray-700/60 bg-gray-950 shadow-xl"
      data-testid="game-canvas"
      aria-label="Isometric world view"
    />
  );
}