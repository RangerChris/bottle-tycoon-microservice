// Truck journey runtime: one entry per truck, advanced by the Pixi ticker.
// Pure transport orchestration — the economy fires from callbacks here
// (deliverToPlant on plant arrival), the store stays untouched per-frame.
import { createJourney, stepJourney, routeBetween, type JourneyState, type JourneyPaths } from '../world/truckSim';
import { buildRoadGraph, roadStopFor, type RoadGraph } from '../world/roadGraph';
import type { WorldMap, TilePos, TruckPhase } from '../world/types';
import { parseTileKey } from '../world/types';

type Entry = {
  journey: JourneyState | null; // null = parked
  parkedAt: TilePos | null;
};

const entries = new Map<string, Entry>(); // key: String(truckId)
let map: WorldMap | null = null;
let graph: RoadGraph | null = null;
let onArrivePlant: ((truckId: number | string) => void) | null = null;

// Call whenever the map may have changed; rebuilds the road graph.
export function configureJourneys(worldMap: WorldMap | null, arrivePlant: (truckId: number | string) => void): void {
  onArrivePlant = arrivePlant;
  if (worldMap && (!map || map.seed !== worldMap.seed)) {
    map = worldMap;
    graph = buildRoadGraph(map);
  }
}

export function hasJourney(truckId: number | string): boolean {
  return entries.has(String(truckId));
}

// Parked trucks (freshly bought) idle at the HQ stop.
export function ensureParked(truckId: number | string): void {
  const key = String(truckId);
  if (entries.has(key) || !map) return;
  entries.set(key, { journey: null, parkedAt: { x: map.hq.x, y: map.hq.y } });
}

export function removeTruck(truckId: number | string): void {
  entries.delete(String(truckId));
}

// Starts a full delivery journey. Returns false when the world can't route
// it (missing location / unroutable) — the caller then falls back to a timer.
export function startJourney(truckId: number | string, recyclerTile: TilePos): boolean {
  if (!map || !graph) return false;
  const hqStop = roadStopFor(map, map.hq.x, map.hq.y);
  const recyclerStop = roadStopFor(map, recyclerTile.x, recyclerTile.y);
  const plantStop = roadStopFor(map, map.plant.x, map.plant.y);
  if (!hqStop || !recyclerStop || !plantStop) return false;

  const toRecycler = routeBetween(graph, hqStop, recyclerStop);
  const toPlant = routeBetween(graph, recyclerStop, plantStop);
  const toHq = routeBetween(graph, plantStop, hqStop);
  if (toRecycler.length === 0 || toPlant.length === 0 || toHq.length === 0) return false;

  const paths: JourneyPaths = { toRecycler, toPlant, toHq };
  entries.set(String(truckId), { journey: createJourney(truckId, paths), parkedAt: null });
  return true;
}

// Advances every journey. Parked trucks stay put.
export function tickJourneys(dtSeconds: number, speedMultiplier: number): void {
  for (const [key, entry] of entries) {
    if (!entry.journey) continue;
    const result = stepJourney(entry.journey, dtSeconds, speedMultiplier);
    entry.journey = result.journey;
    if (result.arrivedAtPlant) {
      onArrivePlant?.(entry.journey.truckId);
    }
    if (result.parked && map) {
      entries.set(key, { journey: null, parkedAt: { x: map.hq.x, y: map.hq.y } });
    }
  }
}

export type TruckRenderState = {
  fx: number;
  fy: number;
  phase: TruckPhase;
  dirX: number;
  dirY: number;
  loaded: boolean;
};

// Renderer-facing snapshot: fractional tile position + orientation + state.
export function journeyRenderStates(): Map<string, TruckRenderState> {
  const out = new Map<string, TruckRenderState>();
  for (const [key, entry] of entries) {
    if (entry.journey) {
      const j = entry.journey;
      const idx = Math.min(j.legIndex, Math.max(0, j.path.length - 2));
      const from = parseTileKey(j.path[idx] ?? '0,0');
      const to = parseTileKey(j.path[idx + 1] ?? j.path[idx] ?? '0,0');
      out.set(key, {
        fx: from.x + (to.x - from.x) * j.legProgress,
        fy: from.y + (to.y - from.y) * j.legProgress,
        phase: j.phase,
        dirX: to.x - from.x,
        dirY: to.y - from.y,
        loaded: j.phase === 'toPlant' || j.phase === 'unloading',
      });
    } else if (entry.parkedAt) {
      out.set(key, { fx: entry.parkedAt.x, fy: entry.parkedAt.y, phase: 'atHq', dirX: 1, dirY: 0, loaded: false });
    }
  }
  return out;
}

export function journeyCount(): number {
  return entries.size;
}