// PixiJS singleton: Application lifecycle, camera pan/zoom, tile picking,
// placement + selection handling. Lives outside React — GameCanvas mounts it
// into a div; per-frame state never flows through React re-renders.
import { Application, Container } from 'pixi.js';
import useWorldStore from '../store/useWorldStore';
import useGameStore from '../store/useGameStore';
import { toScreen, toTile } from '../world/iso';
import { canPlace } from '../world/placement';
import type { PlacementError } from '../world/placement';
import type { WorldBuilding } from '../world/types';
import type { WorldLayers } from './WorldRenderer';
import { createLayers, renderMap, addBuildingSprite, removeBuildingSprite } from './WorldRenderer';
import { updateOverlays } from './draw/overlays';

const MIN_ZOOM = 0.5;
const MAX_ZOOM = 2;
const ZOOM_FACTOR = 1.1;
const DRAG_SLOP_PX = 4; // pointer travel before a press counts as a pan

let app: Application | null = null;
let worldRoot: Container | null = null;
let layers: WorldLayers | null = null;
let host: HTMLElement | null = null;
let renderedSeed: number | null = null;
let drag: { id: number; lastX: number; lastY: number; moved: boolean } | null = null;
let unsubscribe: (() => void) | null = null;
let initPromise: Promise<void> | null = null;
let initGeneration = 0;

// Mounts the engine into a container div. Guards against double-init —
// including the remount race where destroy() runs while a previous init is
// still awaiting app.init() (which would stack two canvases).
export function init(container: HTMLElement): Promise<void> {
  if (initPromise) return initPromise;
  const gen = ++initGeneration;
  initPromise = doInit(container, gen).catch((err) => {
    initPromise = null;
    throw err;
  });
  return initPromise;
}

async function doInit(container: HTMLElement, gen: number): Promise<void> {
  const store = useWorldStore.getState();
  if (!store.map) store.initMap();
  const map = useWorldStore.getState().map;
  if (!map) throw new Error('world map failed to initialise');

  const a = new Application();
  await a.init({
    resizeTo: container,
    background: 0x111827, // gray-900
    antialias: true,
    resolution: window.devicePixelRatio || 1,
    autoDensity: true,
  });
  // Superseded while initializing (destroy/remount happened meanwhile)?
  // Tear this instance down instead of stacking a second canvas.
  if (gen !== initGeneration) {
    a.destroy(true, { children: true });
    return;
  }
  container.appendChild(a.canvas);

  worldRoot = new Container();
  a.stage.addChild(worldRoot);

  layers = createLayers();
  worldRoot.addChild(layers.root);
  renderMap(map, layers);
  renderedSeed = map.seed;

  attachInput(container);
  centerOn(map.hq.x, map.hq.y);

  // Store-driven redraws: buildings + overlays + reseeded maps.
  unsubscribe = useWorldStore.subscribe(() => syncFromStore());
  syncFromStore();

  // Debug/automation hook (DebugPanel uses this later too).
  (window as unknown as Record<string, unknown>).__world = {
    get camera() {
      return useWorldStore.getState().camera;
    },
    get map() {
      return useWorldStore.getState().map;
    },
    zoomBy: (factor: number) => {
      const cam = useWorldStore.getState().camera;
      useWorldStore.getState().setCamera({ zoom: clampZoom(cam.zoom * factor) });
      applyCamera();
    },
  };
}

export function destroy(): void {
  if (unsubscribe) {
    unsubscribe();
    unsubscribe = null;
  }
  detachInput();
  initGeneration++; // any in-flight init is now superseded and will self-destruct
  initPromise = null; // a destroy always allows a fresh init
  if (!app) return;
  app.destroy(true, { children: true });
  app = null;
  worldRoot = null;
  layers = null;
  drag = null;
  renderedSeed = null;
}

// Re-renders the map if the store's map changed (e.g. reseeded), then
// reconciles building sprites and overlays with the world store.
function syncFromStore(): void {
  if (!layers) return;
  const s = useWorldStore.getState();
  if (s.map && s.map.seed !== renderedSeed) {
    renderMap(s.map, layers);
    renderedSeed = s.map.seed;
  }
  syncBuildings(s.buildings);
  updateOverlays(layers, {
    map: s.map as NonNullable<typeof s.map>,
    buildMode: s.buildMode,
    hoveredTile: s.hoveredTile,
    selectedBuilding: selectedBuildingSprite(s),
  });
}

function selectedBuildingSprite(s: ReturnType<typeof useWorldStore.getState>): { id: string; x: number; y: number } | null {
  const sel = s.selectedEntity;
  if (!sel || sel.kind !== 'recycler') return null;
  const id = `recycler-${sel.id}`;
  const b = s.buildings[id];
  return b ? { id, x: b.x, y: b.y } : null;
}

// Reconcile building sprites with the store (id-keyed by container label).
function syncBuildings(buildings: Record<string, WorldBuilding>): void {
  if (!layers) return;
  const wanted = new Set(Object.keys(buildings));
  for (const c of [...layers.buildings.children]) {
    const label = (c as unknown as { label?: string }).label;
    if (typeof label === 'string' && label.startsWith('recycler-') && !wanted.has(label)) {
      removeBuildingSprite(layers, label);
    }
  }
  for (const [id, b] of Object.entries(buildings)) {
    addBuildingSprite(layers, b.kind, b.x, b.y, id);
  }
}

// --- placement ---

const PLACEMENT_REASONS: Record<PlacementError, string> = {
  water: 'Cannot build on water.',
  occupied: 'That tile is already occupied.',
  blocked: 'Clear the trees first — that tile is forested.',
  'no-road': 'Recyclers must be placed next to a road so trucks can reach them.',
  'out-of-bounds': 'That tile is outside the world.',
};

async function attemptPlacement(tile: { x: number; y: number }): Promise<void> {
  const world = useWorldStore.getState();
  if (!world.map) return;
  const verdict = canPlace(world.map, tile.x, tile.y);
  if (!verdict.ok) {
    useGameStore.getState().addLog(PLACEMENT_REASONS[verdict.reason], 'warning');
    return;
  }
  const ok = await useGameStore.getState().buyRecyclerAt(tile);
  if (ok) {
    useWorldStore.getState().setBuildMode('none');
  }
}

// --- camera ---

export function centerOn(tileX: number, tileY: number): void {
  const s = toScreen(tileX, tileY);
  const view = viewSize();
  useWorldStore.getState().setCamera({ x: view.w / 2 - s.sx, y: view.h / 2 - s.sy });
  applyCamera();
}

function viewSize(): { w: number; h: number } {
  const canvas = app?.canvas;
  if (canvas) {
    // autoDensity makes canvas CSS size match logical pixels.
    return { w: canvas.clientWidth, h: canvas.clientHeight };
  }
  const el = host;
  if (el) return { w: el.clientWidth, h: el.clientHeight };
  return { w: 800, h: 600 };
}

function applyCamera(): void {
  if (!worldRoot) return;
  const cam = useWorldStore.getState().camera;
  worldRoot.position.set(cam.x, cam.y);
  worldRoot.scale.set(cam.zoom);
}

function onWheel(event: WheelEvent): void {
  event.preventDefault();
  const store = useWorldStore.getState();
  const cam = store.camera;
  const next = clampZoom(event.deltaY < 0 ? cam.zoom * ZOOM_FACTOR : cam.zoom / ZOOM_FACTOR);
  if (next === cam.zoom) return;
  // Anchor zoom on the cursor: the world point under it stays put.
  const rect = rectOf();
  const mx = event.clientX - rect.left;
  const my = event.clientY - rect.top;
  const wx = (mx - cam.x) / cam.zoom;
  const wy = (my - cam.y) / cam.zoom;
  store.setCamera({ zoom: next, x: mx - wx * next, y: my - wy * next });
  applyCamera();
}

function clampZoom(z: number): number {
  return Math.max(MIN_ZOOM, Math.min(MAX_ZOOM, z));
}

// Pointer coords are client-space; canvas offset within the page is the gap.
function rectOf(): { left: number; top: number } {
  const canvas = app?.canvas;
  if (canvas) return canvas.getBoundingClientRect();
  if (host) return host.getBoundingClientRect();
  return { left: 0, top: 0 };
}

// --- pointer: pan on drag, hover ghost, click-to-place/select ---

function onPointerDown(event: PointerEvent): void {
  if (event.button !== 0 && event.button !== 1) return;
  drag = { id: event.pointerId, lastX: event.clientX, lastY: event.clientY, moved: false };
}

function onPointerMove(event: PointerEvent): void {
  if (drag && drag.id === event.pointerId) {
    const dx = event.clientX - drag.lastX;
    const dy = event.clientY - drag.lastY;
    if (!drag.moved && Math.hypot(dx, dy) < DRAG_SLOP_PX) return;
    drag.moved = true;
    drag.lastX = event.clientX;
    drag.lastY = event.clientY;
    const cam = useWorldStore.getState().camera;
    useWorldStore.getState().setCamera({ x: cam.x + dx, y: cam.y + dy });
    applyCamera();
    return;
  }
  // Hover ghost while in placement mode.
  const s = useWorldStore.getState();
  if (s.buildMode !== 'recycler' || !s.map) return;
  const tile = tileUnderPointer(event);
  const hovered = s.hoveredTile;
  if (tile && (!hovered || hovered.x !== tile.x || hovered.y !== tile.y)) {
    useWorldStore.getState().setHoveredTile(tile);
  }
}

function onPointerUp(event: PointerEvent): void {
  if (!drag || drag.id !== event.pointerId) return;
  const wasClick = !drag.moved;
  drag = null;
  if (!wasClick) return;

  const s = useWorldStore.getState();
  const tile = tileUnderPointer(event);
  if (!tile || !s.map) return;

  if (s.buildMode === 'recycler') {
    void attemptPlacement(tile);
    return;
  }

  // Selection: clicking a recycler building selects it; elsewhere clears.
  const inBounds = tile.x >= 0 && tile.y >= 0 && tile.x < s.map.width && tile.y < s.map.height;
  const buildingId = inBounds ? s.map.tiles[tile.y * s.map.width + tile.x].buildingId : null;
  if (buildingId && buildingId.startsWith('recycler-')) {
    useWorldStore.getState().selectEntity({ kind: 'recycler', id: buildingId.slice('recycler-'.length) });
  } else {
    useWorldStore.getState().selectEntity(null);
  }
}

function onKeyDown(event: KeyboardEvent): void {
  if (event.key !== 'Escape') return;
  const s = useWorldStore.getState();
  if (s.buildMode !== 'none') {
    s.setBuildMode('none');
    s.setHoveredTile(null);
  }
}

// Screen → world → tile; the single picking code path (world/iso.ts math).
export function pickAt(event: PointerEvent): void {
  const tile = tileUnderPointer(event);
  if (tile) useWorldStore.getState().setHoveredTile(tile);
}

function tileUnderPointer(event: PointerEvent): { x: number; y: number } | null {
  const rect = rectOf();
  const cam = useWorldStore.getState().camera;
  return toTile((event.clientX - rect.left - cam.x) / cam.zoom, (event.clientY - rect.top - cam.y) / cam.zoom);
}

function attachInput(container: HTMLElement): void {
  host = container;
  container.addEventListener('wheel', onWheel, { passive: false });
  container.addEventListener('pointerdown', onPointerDown);
  container.addEventListener('pointermove', onPointerMove);
  container.addEventListener('pointerup', onPointerUp);
  container.addEventListener('pointerleave', onPointerUp);
  window.addEventListener('keydown', onKeyDown);
}

export function detachInput(): void {
  if (!host) return;
  host.removeEventListener('wheel', onWheel);
  host.removeEventListener('pointerdown', onPointerDown);
  host.removeEventListener('pointermove', onPointerMove);
  host.removeEventListener('pointerup', onPointerUp);
  host.removeEventListener('pointerleave', onPointerUp);
  window.removeEventListener('keydown', onKeyDown);
  host = null;
}