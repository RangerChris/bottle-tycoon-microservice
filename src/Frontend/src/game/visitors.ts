// Visitor walker runtime (cosmetic): visitors walk along roads from a map
// edge to the recycler's stop tile, then queue beside it until the economy
// processes them. Purely visual — the store owns the actual queue.
import { bfsPath, buildRoadGraph, roadStopFor, type RoadGraph } from '../world/roadGraph';
import type { WorldMap, TilePos } from '../world/types';
import { tileKey } from '../world/types';

const WALK_SPEED_TILES_PER_SEC = 1.6;

type VisitorEntry = {
  path: string[];
  legIndex: number;
  legProgress: number;
  state: 'walking' | 'queueing';
  stop: TilePos;
  queueIndex: number; // standing offset slot around the stop
};

const entries = new Map<string, VisitorEntry>(); // key: `${recyclerId}-${visitorId}`
let map: WorldMap | null = null;
let graph: RoadGraph | null = null;
let edgeRoads: string[] = [];
// Fired when a walker physically reaches its recycler stop; the store uses
// this to start that visitor's deposits.
let onArriveStop: ((key: string) => void) | null = null;

export function setVisitorArrivalCallback(cb: ((key: string) => void) | null): void {
  onArriveStop = cb;
}

export function configureVisitors(worldMap: WorldMap | null): void {
  if (!worldMap || (map && map.seed === worldMap.seed)) return;
  map = worldMap;
  graph = buildRoadGraph(map);
  edgeRoads = [];
  // Map changed (reseed): stale walker paths point at the old map.
  entries.clear();
  for (let y = 0; y < map.height; y++) {
    for (let x = 0; x < map.width; x++) {
      if (!map.tiles[y * map.width + x].road) continue;
      if (x === 0 || y === 0 || x === map.width - 1 || y === map.height - 1) {
        edgeRoads.push(tileKey(x, y));
      }
    }
  }
}

export function hasVisitor(key: string): boolean {
  return entries.has(key);
}

export function visitorKeys(): string[] {
  return [...entries.keys()];
}

// Spawns a walker from a random map-edge road tile to the recycler stop.
// Returns false when the world can't represent the walk (no map/route) —
// the caller should release the visitor's deposits immediately.
export function spawnVisitor(key: string, recyclerTile: TilePos): boolean {
  if (!map || !graph) return false;
  const stopKey = roadStopFor(map, recyclerTile.x, recyclerTile.y);
  if (!stopKey) return false;
  const stop = parseKey(stopKey);
  const start = edgeRoads.length > 0 ? edgeRoads[Math.floor(Math.random() * edgeRoads.length)] : null;
  let path: string[] = [];
  if (start) {
    const r = bfsPath(graph, start, stopKey);
    if (r.reachable) path = r.path;
  }
  const queueIndex = [...entries.values()].filter((e) => e.stop.x === stop.x && e.stop.y === stop.y).length;
  entries.set(key, { path, legIndex: 0, legProgress: 0, state: 'walking', stop, queueIndex });
  return true;
}

export function despawnVisitor(key: string): void {
  entries.delete(key);
}

export function tickVisitors(dtSeconds: number, speedMultiplier: number): void {
  for (const [key, e] of entries) {
    if (e.state !== 'walking') continue;
    let distance = WALK_SPEED_TILES_PER_SEC * speedMultiplier * dtSeconds;
    const segments = e.path.length - 1;
    while (distance > 0 && e.legIndex < segments) {
      const remaining = 1 - e.legProgress;
      if (distance < remaining) {
        e.legProgress += distance;
        distance = 0;
      } else {
        distance -= remaining;
        e.legIndex++;
        e.legProgress = 0;
      }
    }
    if (e.legIndex >= segments) {
      e.state = 'queueing';
      onArriveStop?.(key);
    }
  }
}

export type VisitorRenderState = { fx: number; fy: number; queued: boolean; queueIndex: number };

export function visitorRenderStates(): Map<string, VisitorRenderState> {
  const out = new Map<string, VisitorRenderState>();
  for (const [key, e] of entries) {
    if (e.state === 'walking' && e.path.length >= 2) {
      const idx = Math.min(e.legIndex, e.path.length - 2);
      const from = parseKey(e.path[idx]);
      const to = parseKey(e.path[idx + 1]);
      out.set(key, {
        fx: from.x + (to.x - from.x) * e.legProgress,
        fy: from.y + (to.y - from.y) * e.legProgress,
        queued: false,
        queueIndex: e.queueIndex,
      });
    } else {
      // Queued: stand beside the stop in a small arc.
      const slot = e.queueIndex % 6;
      const offsets = [
        { dx: 0.3, dy: 0.3 },
        { dx: -0.3, dy: 0.3 },
        { dx: 0.3, dy: -0.3 },
        { dx: -0.3, dy: -0.3 },
        { dx: 0.55, dy: 0 },
        { dx: 0, dy: 0.55 },
      ];
      out.set(key, { fx: e.stop.x + offsets[slot].dx, fy: e.stop.y + offsets[slot].dy, queued: true, queueIndex: e.queueIndex });
    }
  }
  return out;
}

function parseKey(key: string): TilePos {
  const idx = key.indexOf(',');
  return { x: Number(key.slice(0, idx)), y: Number(key.slice(idx + 1)) };
}