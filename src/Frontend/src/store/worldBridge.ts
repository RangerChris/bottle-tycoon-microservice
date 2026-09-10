// Bridge: maps economy state (game store) → world visuals (world store).
// One-way sync of placed recyclers; trucks/visitors visuals join in later sessions.
import useGameStore from './useGameStore';
import useWorldStore from './useWorldStore';
import type { WorldBuilding } from '../world/types';

export function initWorldBridge(): () => void {
  const sync = () => {
    const world = useWorldStore.getState();
    if (!world.map) return;
    const game = useGameStore.getState();

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

    // Remove world recyclers that no longer exist in the economy store.
    for (const id of Object.keys(world.buildings)) {
      if (id.startsWith('recycler-') && !wanted.has(id)) {
        useWorldStore.getState().removeBuilding(id);
      }
    }
    // Add new ones (idempotent).
    for (const [id, building] of wanted) {
      if (!useWorldStore.getState().buildings[id]) {
        useWorldStore.getState().addBuilding(building);
      }
    }
  };

  sync();
  return useGameStore.subscribe(sync);
}