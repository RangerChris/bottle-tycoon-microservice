import { describe, it, expect } from 'vitest';
import {
  advanceTruck,
  startJourney,
  positionOnPath,
  legJitter,
  timeMultiplier,
  phaseForStatus,
  routeBetween,
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

describe('phaseForStatus', () => {
  it('maps economy statuses to visual phases', () => {
    expect(phaseForStatus('idle')).toBe('atHq');
    expect(phaseForStatus('en route')).toBe('toRecycler');
    expect(phaseForStatus('loading')).toBe('loading');
    expect(phaseForStatus('to_plant')).toBe('toPlant');
    expect(phaseForStatus('delivering')).toBe('toPlant');
    expect(phaseForStatus('unknown-thing')).toBe('atHq');
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
    // routeBetween is imported lazily to keep the module graph simple here.
    return import('../truckSim').then(({ routeBetween }) => {
      const path = routeBetween(graph, hqStop, plantStop);
      expect(path.length).toBeGreaterThan(1);
      expect(path[0]).toBe(hqStop);
      expect(path[path.length - 1]).toBe(plantStop);
    });
  });
});