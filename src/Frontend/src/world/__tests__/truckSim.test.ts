import { describe, it, expect } from 'vitest';
import {
  advanceTruck,
  startJourney,
  positionOnPath,
  legJitter,
  timeMultiplier,
  routeBetween,
  createJourney,
  stepJourney,
  operatingCostFor,
} from '../truckSim';
import { generateMap } from '../mapgen';
import { buildRoadGraph, roadStopFor } from '../roadGraph';

describe('timeMultiplier', () => {
  it('matches the economy multipliers', () => {
    expect(timeMultiplier(1)).toBe(0);
    expect(timeMultiplier(2)).toBe(1);
    expect(timeMultiplier(3)).toBe(2);
    expect(timeMultiplier(4)).toBe(4);
    expect(timeMultiplier(5)).toBe(5);
  });

  it('falls back to 1x for unknown levels', () => {
    expect(timeMultiplier(99)).toBe(1);
  });
});

describe('operatingCostFor', () => {
  it('is distance × 0.5 × 1.25^level, rounded to 2 decimals', () => {
    expect(operatingCostFor(10, 0)).toBe(5);
    expect(operatingCostFor(10, 2)).toBe(7.81);
    expect(operatingCostFor(24, 3)).toBe(23.44);
  });

  it('is free at zero distance (degenerate routes)', () => {
    expect(operatingCostFor(0, 3)).toBe(0);
  });

  it('rounds to 2 decimals to keep payloads tidy', () => {
    expect(operatingCostFor(7, 1)).toBeCloseTo(4.38, 5);
    expect(Number(operatingCostFor(7, 1).toFixed(2))).toBe(operatingCostFor(7, 1));
  });
});

describe('legJitter', () => {
  it('is deterministic per seed+leg and within ±15%', () => {
    expect(legJitter(7, 3)).toBe(legJitter(7, 3));
    for (let leg = 0; leg < 20; leg++) {
      const j = legJitter(123, leg);
      expect(j).toBeGreaterThanOrEqual(0.85);
      expect(j).toBeLessThanOrEqual(1.15);
    }
  });
});

describe('advanceTruck', () => {
  const path = ['0,0', '1,0', '2,0', '3,0']; // 3 segments

  it('does not move while paused', () => {
    const v = startJourney(1, path, 'toRecycler');
    const r = advanceTruck(v, 10, 0);
    expect(r.arrived).toBe(false);
    expect(r.visual.legIndex).toBe(0);
    expect(r.visual.legProgress).toBe(0);
  });

  it('advances by speed × dt and arrives at the end', () => {
    const v = startJourney(1, path, 'toRecycler');
    // 2 tiles/s at 1x → 0.5s per tile; 3 segments → 1.5s total.
    const half = advanceTruck(v, 0.75, 1);
    expect(half.arrived).toBe(false);
    const done = advanceTruck(half.visual, 5, 1);
    expect(done.arrived).toBe(true);
    expect(done.visual.legIndex).toBe(2);
    expect(done.visual.legProgress).toBe(1);
  });

  it('advances faster at higher multipliers', () => {
    const v = startJourney(1, path, 'toPlant');
    const at5x = advanceTruck(v, 0.25, 5); // 0.25s × 2 t/s × 5 = 2.5 tiles
    expect(at5x.visual.legIndex).toBe(2);
    expect(at5x.visual.legProgress).toBe(0.5);
    expect(at5x.arrived).toBe(false);
  });

  it('handles empty paths as parked', () => {
    const v = startJourney(1, [], 'atHq');
    const r = advanceTruck(v, 1, 1);
    expect(r.arrived).toBe(false);
  });
});

describe('positionOnPath', () => {
  it('interpolates between path waypoints', () => {
    const p = positionOnPath(['2,2', '3,2'], 0, 0.5);
    expect(p.fx).toBe(2.5);
    expect(p.fy).toBe(2);
  });

  it('handles parked trucks with empty paths', () => {
    const p = positionOnPath([], 0, 0);
    expect(p.fx).toBe(0);
    expect(p.fy).toBe(0);
  });
});

describe('routeBetween (integration with mapgen)', () => {
  it('routes HQ → recycler stop and recycler stop → plant stop', () => {
    const map = generateMap(42);
    const graph = buildRoadGraph(map);
    // A placed building's stop connects; simulate placing a recycler by
    // using the plant's own stop for route sanity (no extra roads needed).
    const hqStop = roadStopFor(map, map.hq.x, map.hq.y) as string;
    const plantStop = roadStopFor(map, map.plant.x, map.plant.y) as string;
    const path = routeBetween(graph, hqStop, plantStop);
    expect(path.length).toBeGreaterThan(1);
    expect(path[0]).toBe(hqStop);
    expect(path[path.length - 1]).toBe(plantStop);
  });
});

describe('stepJourney (full delivery state machine)', () => {
  const paths = {
    toRecycler: ['0,0', '1,0', '2,0'], // 2 segments
    toPlant: ['2,0', '2,1', '2,2'], // 2 segments
    toHq: ['2,2', '1,2'], // 1 segment
  };

  function drive(journey: ReturnType<typeof createJourney>, dt: number, mult: number, maxSteps = 5000) {
    const events: string[] = [];
    let state = journey;
    for (let i = 0; i < maxSteps; i++) {
      const r = stepJourney(state, dt, mult);
      state = r.journey;
      if (r.arrivedAtPlant) events.push('arrivedAtPlant');
      if (r.parked) {
        events.push('parked');
        break;
      }
    }
    return { state, events };
  }

  it('runs drive → load → drive → unload → return and parks', () => {
    const j = createJourney(1, paths);
    const { state, events } = drive(j, 0.1, 1);
    expect(events).toEqual(['arrivedAtPlant', 'parked']);
    expect(state.phase).toBe('atHq');
    expect(state.path).toEqual([]);
  });

  it('fires arrivedAtPlant exactly once', () => {
    const j = createJourney(1, paths);
    // Drive past the plant arrival with extra steps.
    const { state, events } = drive(j, 0.1, 1);
    expect(state.phase).toBe('atHq');
    expect(events.filter((e) => e === 'arrivedAtPlant').length).toBe(1);
  });

  it('freezes entirely while paused (multiplier 0)', () => {
    const j = createJourney(1, paths);
    const r = stepJourney(j, 10, 0);
    expect(r.journey.phase).toBe('toRecycler');
    expect(r.journey.legIndex).toBe(0);
    expect(r.journey.legProgress).toBe(0);
    expect(r.arrivedAtPlant).toBe(false);
  });

  it('loading timer waits through multiple ticks while loading', () => {
    const j = createJourney(1, paths);
    let state = j;
    // Drive to the recycler (2 segments = 1s at 2 tiles/s), small steps.
    for (let i = 0; i < 40; i++) {
      const r = stepJourney(state, 0.05, 1);
      state = r.journey;
    }
    expect(state.phase).toBe('loading');
    expect(state.timer).toBeGreaterThan(0);
    // Loading had 1.5s left at the 2.0s mark; 1.4s more keeps it loading.
    for (let i = 0; i < 28; i++) {
      const r = stepJourney(state, 0.05, 1);
      state = r.journey;
    }
    expect(state.phase).toBe('loading');
    for (let i = 0; i < 4; i++) {
      const r = stepJourney(state, 0.05, 1);
      state = r.journey;
    }
    expect(state.phase).toBe('toPlant');
  });

  it('handles degenerate empty paths without hanging', () => {
    const j = createJourney(1, { toRecycler: [], toPlant: [], toHq: [] });
    let state = j;
    let plantEvents = 0;
    // Empty drives are instant, but load+unload timers still run: 2.5+2.0s.
    for (let i = 0; i < 60; i++) {
      const r = stepJourney(state, 0.1, 1);
      state = r.journey;
      if (r.arrivedAtPlant) plantEvents++;
      if (r.parked) break;
    }
    expect(state.phase).toBe('atHq');
    expect(plantEvents).toBe(1);
  });
});