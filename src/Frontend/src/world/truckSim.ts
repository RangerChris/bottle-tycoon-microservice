// Truck movement over the road graph: traversal timing + lifecycle mapping.
// Pure math only — Pixi repositions containers from these outputs.
import type { TruckVisual, TruckPhase } from './types';
import { bfsPath, type RoadGraph } from './roadGraph';

export const BASE_SPEED_TILES_PER_SEC = 2;
export const LOAD_SECONDS = 2.5;
export const UNLOAD_SECONDS = 2.0;

// Existing economy time multipliers (useGameStore): pause freezes trucks.
export const TIME_MULTIPLIERS: Record<number, number> = { 1: 0, 2: 1, 3: 2, 4: 4, 5: 5 };

export function timeMultiplier(timeLevel: number): number {
  return TIME_MULTIPLIERS[timeLevel] ?? 1;
}

// Distance-based operating cost for a delivery: each road tile costs 0.5
// credits at level 0, +25% per truck upgrade level (mirrors capacity math).
export const COST_PER_TILE = 0.5;
export const COST_LEVEL_FACTOR = 1.25;

export function operatingCostFor(distanceTiles: number, level: number): number {
  const cost = distanceTiles * COST_PER_TILE * Math.pow(COST_LEVEL_FACTOR, level);
  return Math.round(cost * 100) / 100;
}

// Builds a truck visual for a new journey: hqStop → targetStop on road tiles.
export function startJourney(truckId: number | string, path: string[], phase: TruckPhase): TruckVisual {
  return { truckId, path, legIndex: 0, legProgress: 0, phase };
}

// Per-leg seeded jitter (±15%) for visual variance; deterministic per leg.
export function legJitter(seed: number, legIndex: number): number {
  let a = (seed ^ (legIndex * 0x9e3779b9)) >>> 0;
  a = (a + 0x6d2b79f5) | 0;
  let t = Math.imul(a ^ (a >>> 15), 1 | a);
  t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
  const v = ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  return 0.85 + v * 0.3; // 0.85..1.15
}

// Advance a truck along its path. Returns the (possibly new) visual plus
// whether it arrived at the end of the path this tick.
export type AdvanceResult = { visual: TruckVisual; arrived: boolean };

export function advanceTruck(visual: TruckVisual, dtSeconds: number, speedMultiplier: number): AdvanceResult {
  if (visual.path.length < 2 || speedMultiplier === 0) {
    return { visual, arrived: false };
  }
  const segments = visual.path.length - 1;
  let legIndex = visual.legIndex;
  let legProgress = visual.legProgress;

  let distance = BASE_SPEED_TILES_PER_SEC * speedMultiplier * dtSeconds;
  // Note: jitter affects total journey feel, applied per segment by the
  // renderer via legJitter(); here we advance by raw distance for simplicity.
  while (distance > 0 && legIndex < segments) {
    const remaining = 1 - legProgress;
    if (distance < remaining) {
      legProgress += distance;
      distance = 0;
    } else {
      distance -= remaining;
      legIndex++;
      legProgress = 0;
    }
  }

  const arrived = legIndex >= segments;
  if (arrived) {
    legIndex = segments - 1;
    legProgress = 1;
  }
  return { visual: { ...visual, legIndex, legProgress }, arrived };
}

// Position along the path as a fractional tile coordinate (for rendering).
export function positionOnPath(path: string[], legIndex: number, legProgress: number): { fx: number; fy: number } {
  if (path.length === 0) return { fx: 0, fy: 0 };
  const idx = Math.min(legIndex, path.length - 2);
  const from = parseKey(path[idx]);
  const to = parseKey(path[idx + 1] ?? path[idx]);
  return {
    fx: from.x + (to.x - from.x) * legProgress,
    fy: from.y + (to.y - from.y) * legProgress,
  };
}

function parseKey(key: string): { x: number; y: number } {
  const idx = key.indexOf(',');
  return { x: Number(key.slice(0, idx)), y: Number(key.slice(idx + 1)) };
}

// Convenience: route a truck from one building's stop to another's.
export function routeBetween(graph: RoadGraph, fromStop: string, toStop: string): string[] {
  const result = bfsPath(graph, fromStop, toStop);
  return result.reachable ? result.path : [];
}

// --- full delivery journey state machine ---
// HQ → recycler (drive) → load (timer) → plant (drive) → unload (timer,
// fires the plant delivery) → HQ (drive) → parked.

export type JourneyPaths = {
  toRecycler: string[];
  toPlant: string[];
  toHq: string[];
};

export type JourneyState = {
  truckId: number | string;
  phase: TruckPhase | 'toHq';
  paths: JourneyPaths;
  path: string[]; // active path
  legIndex: number;
  legProgress: number;
  timer: number; // countdown for loading/unloading phases
};

export type JourneyStepResult = {
  journey: JourneyState;
  arrivedAtPlant: boolean; // true on exactly one tick
  parked: boolean; // true once back at HQ
};

export function createJourney(truckId: number | string, paths: JourneyPaths): JourneyState {
  return {
    truckId,
    phase: 'toRecycler',
    paths,
    path: paths.toRecycler,
    legIndex: 0,
    legProgress: 0,
    timer: 0,
  };
}

export function stepJourney(journey: JourneyState, dtSeconds: number, speedMultiplier: number): JourneyStepResult {
  let { phase, path, legIndex, legProgress, timer } = journey;
  let arrivedAtPlant = false;
  let parked = false;

  const advance = (): boolean => {
    // Returns true when the active path is completed this tick.
    if (path.length < 2) return true;
    const segments = path.length - 1;
    let distance = BASE_SPEED_TILES_PER_SEC * speedMultiplier * dtSeconds;
    while (distance > 0 && legIndex < segments) {
      const remaining = 1 - legProgress;
      if (distance < remaining) {
        legProgress += distance;
        distance = 0;
      } else {
        distance -= remaining;
        legIndex++;
        legProgress = 0;
      }
    }
    if (legIndex >= segments) {
      legIndex = segments - 1;
      legProgress = 1;
      return true;
    }
    return false;
  };

  switch (phase) {
    case 'toRecycler':
      if (advance()) {
        phase = 'loading';
        timer = LOAD_SECONDS;
      }
      break;
    case 'loading':
      timer -= dtSeconds * speedMultiplier;
      if (timer <= 0) {
        phase = 'toPlant';
        path = journey.paths.toPlant;
        legIndex = 0;
        legProgress = 0;
      }
      break;
    case 'toPlant':
      if (advance()) {
        arrivedAtPlant = true;
        phase = 'unloading';
        timer = UNLOAD_SECONDS;
      }
      break;
    case 'unloading':
      timer -= dtSeconds * speedMultiplier;
      if (timer <= 0) {
        phase = 'toHq';
        path = journey.paths.toHq;
        legIndex = 0;
        legProgress = 0;
      }
      break;
    case 'toHq':
      if (advance()) {
        phase = 'atHq';
        path = [];
        legIndex = 0;
        legProgress = 0;
        parked = true;
      }
      break;
    default:
      break; // atHq: parked, nothing to do
  }

  return { journey: { ...journey, phase, path, legIndex, legProgress, timer }, arrivedAtPlant, parked };
}