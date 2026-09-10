// PixiJS singleton: Application lifecycle, camera pan/zoom, tile picking,
// placement + selection handling. Lives outside React — GameCanvas mounts it
// into a div; per-frame state never flows through React re-renders.
import { Application, Container, Graphics } from 'pixi.js';
import useWorldStore from '../store/useWorldStore';
import useGameStore from '../store/useGameStore';
import { toScreen, toTile } from '../world/iso';
import { canPlace } from '../world/placement';
import type { PlacementError } from '../world/placement';
import type { WorldBuilding } from '../world/types';
import type { WorldLayers } from './WorldRenderer';
import { createLayers, renderMap, addBuildingSprite, removeBuildingSprite } from './WorldRenderer';
import { updateOverlays } from './draw/overlays';
import { createTruckSprite, updateTruckSprite } from './draw/truckSprite';
import { createVisitorSprite, updateVisitorSprite } from './draw/visitorSprite';
import { updateStatusBars } from './draw/statusBars';
import { tickJourneys, journeyRenderStates } from './journeys';
import { configureVisitors, tickVisitors, visitorRenderStates } from './visitors';
import { timeMultiplier } from '../world/truckSim';
import { toScreen as toScreenPos } from '../world/iso';

// Recycler capacity curve (matches the economy store's calculateCapacity).
const RECYCLER_BASE_CAPACITY = 100;
const CAPACITY_LEVEL_FACTOR = 1.25;

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
  // Default to an HQ view, but don't clobber a restored camera (localStorage).
  const cam = useWorldStore.getState().camera;
  if (cam.x === 0 && cam.y === 0 && cam.zoom === 1) centerOn(map.hq.x, map.hq.y);

  // Store-driven redraws: buildings + overlays + reseeded maps.
  unsubscribe = useWorldStore.subscribe(() => syncFromStore());
  syncFromStore();

  // World clock: trucks, visitors (per-frame state stays out of React).
  let elapsed = 0;
  a.ticker.add((tk) => {
    const dt = tk.deltaMS / 1000;
    elapsed += dt;
    const mult = timeMultiplier(useGameStore.getState().timeLevel);
    tickJourneys(dt, mult);
    tickVisitors(dt, mult);
    renderTrucks();
    renderVisitors(elapsed);
    renderPlantUnloadFx(elapsed);
  });

  // Debug/automation hook (DebugPanel uses this later too).
  (window as unknown as Record<string, unknown>).__world = {
    get camera() {
      return useWorldStore.getState().camera;
    },
    get map() {
      return useWorldStore.getState().map;
    },
    get trucks() {
      return Object.fromEntries(journeyRenderStates());
    },
    zoomBy: (factor: number) => {
      const cam = useWorldStore.getState().camera;
      useWorldStore.getState().setCamera({ zoom: clampZoom(cam.zoom * factor) });
      applyCamera();
    },
    centerOn: (tileX: number, tileY: number) => centerOn(tileX, tileY),
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
  updateStatusBarsFor(s.buildings);
}

// Capacity bars above each placed recycler (data from the economy store).
function updateStatusBarsFor(buildings: Record<string, WorldBuilding>): void {
  if (!layers) return;
  const game = useGameStore.getState();
  const items = [];
  for (const [id, b] of Object.entries(buildings)) {
    if (b.kind !== 'recycler' || !b.entityId) continue;
    const r = game.recyclers.find((x) => String(x.id) === String(b.entityId));
    if (!r) continue;
    const total = r.currentBottles.glass + r.currentBottles.metal + r.currentBottles.plastic;
    const capacity = Math.floor(RECYCLER_BASE_CAPACITY * Math.pow(CAPACITY_LEVEL_FACTOR, r.level));
    items.push({ key: id, x: b.x, y: b.y, fill: capacity > 0 ? total / capacity : 0 });
  }
  updateStatusBars(layers.status, items);
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

// Reconcile + reposition truck sprites from the journey runtime each frame.
function renderTrucks(): void {
  if (!layers) return;
  const states = journeyRenderStates();
  const wanted = new Set(states.keys());
  for (const c of [...layers.units.children]) {
    const label = (c as unknown as { label?: string }).label;
    if (typeof label === 'string' && label.startsWith('truck-') && !wanted.has(label)) {
      c.destroy({ children: true });
    }
  }
  for (const [key, s] of states) {
    let sprite = layers.units.children.find(
      (c) => (c as unknown as { label?: string }).label === `truck-${key}`,
    );
    if (!sprite) {
      sprite = createTruckSprite();
      (sprite as unknown as { label: string }).label = `truck-${key}`;
      layers.units.addChild(sprite);
    }
    const pos = toScreenPos(s.fx, s.fy);
    sprite.position.set(pos.sx, pos.sy + 16); // tile center
    updateTruckSprite(sprite, s.phase, s.dirX, s.dirY, s.loaded);
  }
}

// Visitor walkers (cosmetic) reconciled from the visitor runtime.
function renderVisitors(time: number): void {
  if (!layers) return;
  const states = visitorRenderStates();
  const wanted = new Set(states.keys());
  for (const c of [...layers.units.children]) {
    const label = (c as unknown as { label?: string }).label;
    if (typeof label === 'string' && label.startsWith('visitor-') && !wanted.has(label.slice('visitor-'.length))) {
      c.destroy({ children: true });
    }
  }
  for (const [key, s] of states) {
    let sprite = layers.units.children.find(
      (c) => (c as unknown as { label?: string }).label === `visitor-${key}`,
    );
    if (!sprite) {
      sprite = createVisitorSprite();
      (sprite as unknown as { label: string }).label = `visitor-${key}`;
      layers.units.addChild(sprite);
    }
    const pos = toScreenPos(s.fx, s.fy);
    sprite.position.set(pos.sx, pos.sy + 16);
    updateVisitorSprite(sprite, s.queued, time);
  }
}

// Emerald pulse under the plant while a truck unloads there.
let unloadFx: Graphics | null = null;
function renderPlantUnloadFx(time: number): void {
  if (!layers) return;
  const anyUnloading = [...journeyRenderStates().values()].some((s) => s.phase === 'unloading');
  if (!anyUnloading) {
    if (unloadFx) {
      unloadFx.destroy();
      unloadFx = null;
    }
    return;
  }
  const map = useWorldStore.getState().map;
  if (!map) return;
  const pos = toScreenPos(map.plant.x, map.plant.y);
  if (!unloadFx) {
    unloadFx = new Graphics();
    layers.fx.addChild(unloadFx);
  }
  const alpha = 0.35 + 0.35 * Math.sin(time * 6);
  unloadFx.clear();
  unloadFx.moveTo(pos.sx, pos.sy - 4);
  unloadFx.lineTo(pos.sx + 32 + 4, pos.sy + 16);
  unloadFx.lineTo(pos.sx, pos.sy + 36);
  unloadFx.lineTo(pos.sx - 32 - 4, pos.sy + 16);
  unloadFx.closePath();
  unloadFx.setStrokeStyle({ width: 3, color: 0x10b981, alpha });
  unloadFx.stroke();
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