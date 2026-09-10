// Build/place toolbar overlay: buy buttons that switch the world into
// placement mode. Shown top-left over the canvas.
import useWorldStore from '../../store/useWorldStore';
import useGameStore from '../../store/useGameStore';

const RECYCLER_COST = 500;
const TRUCK_COST = 800;
const MAX_RECYCLERS = 10;
const MAX_TRUCKS = 10;

export default function BuildToolbar() {
  const buildMode = useWorldStore((s) => s.buildMode);
  const setBuildMode = useWorldStore((s) => s.setBuildMode);
  const credits = useGameStore((s: { credits: number }) => s.credits);
  const recyclers = useGameStore((s: { recyclers: unknown[] }) => s.recyclers);
  const trucks = useGameStore((s: { trucks: unknown[] }) => s.trucks);
  const buyTruck = useGameStore((s: { buyTruck: () => void }) => s.buyTruck);

  const recyclerFull = recyclers.length >= MAX_RECYCLERS;
  const truckFull = trucks.length >= MAX_TRUCKS;
  const placing = buildMode === 'recycler';

  return (
    <div className="absolute top-4 left-4 z-10 flex flex-col gap-2" data-testid="build-toolbar">
      <div className="card bg-base-200/90 shadow-xl backdrop-blur">
        <div className="card-body gap-3 p-4">
          <h3 className="card-title text-sm text-emerald-500">🏗️ Build</h3>
          <button
            className={`btn btn-sm w-full text-white no-outline-btn ${placing ? 'btn-warning' : 'bg-blue-600 hover:bg-blue-700'}`}
            disabled={recyclerFull || credits < RECYCLER_COST}
            onClick={() => setBuildMode(placing ? 'none' : 'recycler')}
          >
            {placing ? '🎯 Click a tile next to a road…' : `+ Buy Recycler (${RECYCLER_COST})`}
          </button>
          <button
            className="btn btn-sm w-full bg-blue-600 text-white hover:bg-blue-700 no-outline-btn"
            disabled={truckFull || credits < TRUCK_COST}
            onClick={() => buyTruck()}
          >
            + Buy Truck ({TRUCK_COST})
          </button>
          {placing && (
            <p className="text-xs text-gray-400">Esc to cancel</p>
          )}
        </div>
      </div>
    </div>
  );
}