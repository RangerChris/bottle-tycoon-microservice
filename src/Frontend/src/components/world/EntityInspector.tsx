// Inspector overlay for the selected world entity (right side over canvas).
// Reuses the existing economy cards so all buy/upgrade/sell behavior in one place.
import useWorldStore from '../../store/useWorldStore';
import useGameStore from '../../store/useGameStore';
import RecyclerCard from '../RecyclerCard';
import TruckCard from '../TruckCard';

export default function EntityInspector() {
  const selectedEntity = useWorldStore((s) => s.selectedEntity);
  const selectEntity = useWorldStore((s) => s.selectEntity);
  const recyclers = useGameStore((s: { recyclers: import('../../types').Recycler[] }) => s.recyclers);
  const trucks = useGameStore((s: { trucks: import('../../types').Truck[] }) => s.trucks);

  if (!selectedEntity) return null;

  if (selectedEntity.kind === 'recycler') {
    const recycler = recyclers.find((r) => String(r.id) === String(selectedEntity.id));
    if (!recycler) return null;
    return (
      <div className="absolute top-4 right-4 z-10 w-72" data-testid="entity-inspector">
        <div className="card bg-base-200/90 shadow-xl backdrop-blur">
          <div className="card-body gap-3 p-4">
            <div className="flex items-center justify-between">
              <h3 className="card-title text-sm text-emerald-500">♻️ Recycler</h3>
              <button className="btn btn-ghost btn-xs" onClick={() => selectEntity(null)}>✕</button>
            </div>
            <RecyclerCard recycler={recycler} />
          </div>
        </div>
      </div>
    );
  }

  const truck = trucks.find((t) => String(t.id) === String(selectedEntity.id));
  if (!truck) return null;
  return (
    <div className="absolute top-4 right-4 z-10 w-72" data-testid="entity-inspector">
      <div className="card bg-base-200/90 shadow-xl backdrop-blur">
        <div className="card-body gap-3 p-4">
          <div className="flex items-center justify-between">
            <h3 className="card-title text-sm text-emerald-500">🚚 Truck</h3>
            <button className="btn btn-ghost btn-xs" onClick={() => selectEntity(null)}>✕</button>
          </div>
          <TruckCard truck={truck} />
        </div>
      </div>
    </div>
  );
}