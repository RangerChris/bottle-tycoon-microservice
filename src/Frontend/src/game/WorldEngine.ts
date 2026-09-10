// PixiJS singleton: Application lifecycle, camera pan/zoom, tile picking.
// Lives outside React — GameCanvas mounts it into a div; per-frame state
// (truck positions, camera) never flows through React re-renders.
import { Application, Container } from 'pixi.js';
import useWorldStore from '../store/useWorldStore';
import { toScreen, toTile } from '../world/iso';
import type { WorldLayers } from './WorldRenderer';
import { createLayers, renderMap } from './WorldRenderer';

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

// Mounts the engine into a container div. Idempotent (StrictMode safety).
export async function init(container: HTMLElement): Promise<void> {
  if (app) return;

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
  container.appendChild(a.canvas);

  worldRoot = new Container();
  a.stage.addChild(worldRoot);

  layers = createLayers();
  worldRoot.addChild(layers.root);
  renderMap(map, layers);
  renderedSeed = map.seed;

  attachInput(container);
  centerOn(map.hq.x, map.hq.y);

  // Debug/automation hook (DebugPanel uses this later too).
  (window as unknown as Record<string, unknown>).__world = {
    get camera() {
      return useWorldStore.getState().camera;
    },
    zoomBy: (factor: number) => {
      const cam = useWorldStore.getState().camera;
      useWorldStore.getState().setCamera({ zoom: clampZoom(cam.zoom * factor) });
      applyCamera();
    },
  };
}

export function destroy(): void {
  if (!app) return;
  app.destroy(true, { children: true });
  app = null;
  worldRoot = null;
  layers = null;
  drag = null;
  host = null;
  renderedSeed = null;
}

// Re-renders the map if the store's map changed (e.g. reseeded).
export function syncMap(): void {
  const map = useWorldStore.getState().map;
  if (!map || !layers) return;
  if (renderedSeed === map.seed) return;
  renderMap(map, layers);
  renderedSeed = map.seed;
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

// --- pointer: pan on drag, click-to-pick ---

function onPointerDown(event: PointerEvent): void {
  if (event.button !== 0 && event.button !== 1) return;
  drag = { id: event.pointerId, lastX: event.clientX, lastY: event.clientY, moved: false };
}

function onPointerMove(event: PointerEvent): void {
  if (!drag || drag.id !== event.pointerId) return;
  const dx = event.clientX - drag.lastX;
  const dy = event.clientY - drag.lastY;
  if (!drag.moved && Math.hypot(dx, dy) < DRAG_SLOP_PX) return;
  drag.moved = true;
  drag.lastX = event.clientX;
  drag.lastY = event.clientY;
  const cam = useWorldStore.getState().camera;
  useWorldStore.getState().setCamera({ x: cam.x + dx, y: cam.y + dy });
  applyCamera();
}

function onPointerUp(event: PointerEvent): void {
  if (!drag || drag.id !== event.pointerId) return;
  const wasClick = !drag.moved;
  drag = null;
  if (wasClick) pickAt(event);
}

// Screen → world → tile; the single picking code path (world/iso.ts math).
export function pickAt(event: PointerEvent): void {
  const rect = rectOf();
  const mx = event.clientX - rect.left;
  const my = event.clientY - rect.top;
  const cam = useWorldStore.getState().camera;
  const tile = toTile((mx - cam.x) / cam.zoom, (my - cam.y) / cam.zoom);
  useWorldStore.getState().setHoveredTile(tile);
}

function attachInput(container: HTMLElement): void {
  host = container;
  container.addEventListener('wheel', onWheel, { passive: false });
  container.addEventListener('pointerdown', onPointerDown);
  container.addEventListener('pointermove', onPointerMove);
  container.addEventListener('pointerup', onPointerUp);
  container.addEventListener('pointerleave', onPointerUp);
}

// Center on HQ at first mount.
function centerOnHq(): void {
  const map = useWorldStore.getState().map;
  if (map) centerOn(map.hq.x, map.hq.y);
}

export function tileUnderPointer(event: PointerEvent): { x: number; y: number } | null {
  if (!host) return null;
  const rect = host.getBoundingClientRect();
  const cam = useWorldStore.getState().camera;
  const t = toTile((event.clientX - rect.left - cam.x) / cam.zoom, (event.clientY - rect.top - cam.y) / cam.zoom);
  return t;
}

// Re-exported for GameCanvas centering convenience.
export { viewSize, applyCamera };