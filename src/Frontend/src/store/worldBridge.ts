// Bridge: maps economy state (game store) → world visuals (world store +
// journey runtime). One-way sync; the game store never imports this module.
import useGameStore from './useGameStore';
import useWorldStore from './useWorldStore';
import { configureJourneys, ensureParked, hasJourney, removeTruck, startJourney } from '../game/journeys';
import { configureVisitors, despawnVisitor, hasVisitor, spawnVisitor, visitorKeys } from '../game/visitors';
import { timeMultiplier } from '../world/truckSim';
import type { WorldBuilding } from '../world/types';

export function initWorldBridge(): () => void {
  const prevTruckStatus = new Map<string, string>();

  const sync = () => {
    const world = useWorldStore.getState();
    const game = useGameStore.getState();

    // --- recyclers → world buildings ---
    if (world.map) {
      configureJourneys(world.map, (truckId, distanceTiles) => {
        void useGameStore.getState().deliverToPlant(truckId, distanceTiles);
      });

      const wanted = new Map<string, WorldBuilding>();
      for (const r of game.recyclers) {
        if (!r.location) continue;
        wanted.set(`recycler-${r.id}`, {
          id: `recycler-${r.id}`,
          kind: 'recycler',
          x: r.location.x,
          y: r.location.y,
          entityId: r.id,
        });
      }
      for (const id of Object.keys(useWorldStore.getState().buildings)) {
        if (id.startsWith('recycler-') && !wanted.has(id)) {
          useWorldStore.getState().removeBuilding(id);
        }
      }
      for (const [id, building] of wanted) {
        if (!useWorldStore.getState().buildings[id]) {
          useWorldStore.getState().addBuilding(building);
        }
      }
    }

    // --- trucks → journey runtime ---
    const liveIds = new Set<string>();
    for (const truck of game.trucks) {
      const key = String(truck.id);
      liveIds.add(key);

      const wasStatus = prevTruckStatus.get(key);
      const dispatching = truck.status === 'en route' && truck.targetRecyclerId != null && wasStatus !== 'en route';

      if (dispatching) {
        const recycler = game.recyclers.find((r) => r.id == truck.targetRecyclerId);
        const routed = recycler?.location ? startJourney(truck.id, recycler.location) : false;
        if (!routed) {
          // World can't represent this journey (no location / unroutable):
          // fall back to the legacy fixed travel time.
          const mult = timeMultiplier(game.timeLevel);
          setTimeout(() => {
            const still = useGameStore.getState().trucks.find((t) => t.id == truck.id);
            if (still && still.cargo) void useGameStore.getState().deliverToPlant(truck.id);
          }, Math.max(1000, 10000 / mult));
        }
      }

      // Parked trucks render at the HQ stop.
      if (!hasJourney(truck.id)) {
        ensureParked(truck.id);
      }
      prevTruckStatus.set(key, truck.status);
    }
    // Sold trucks: drop their runtime entry.
    for (const key of [...prevTruckStatus.keys()]) {
      if (!liveIds.has(key)) {
        prevTruckStatus.delete(key);
        removeTruck(key);
      }
    }

    // --- visitors → walker visuals (cosmetic) ---
    if (world.map) {
      configureVisitors(useWorldStore.getState().map);
      const seen = new Set<string>();
      for (const r of game.recyclers) {
        for (const v of r.visitors ?? []) {
          const key = `${r.id}-${v.id}`;
          seen.add(key);
          if (!hasVisitor(key) && r.location) {
            spawnVisitor(key, r.location);
          }
        }
      }
      for (const key of visitorKeys()) {
        if (!seen.has(key)) despawnVisitor(key);
      }
    }
  };

  sync();
  return useGameStore.subscribe(sync);
}