// World-layer Zustand store: map, buildings, camera, build mode, selection.
// Per-frame state (truck positions) lives in the Pixi ticker, NOT here —
// React components must only subscribe to coarse state.
import { create } from 'zustand';
import { immer } from 'zustand/middleware/immer';
import type { WorldMap, WorldBuilding, BuildMode, TilePos, TruckVisual, VisitorVisual } from '../world/types';
import { generateMap } from '../world/mapgen';

export type SelectedEntity = { kind: 'recycler' | 'truck'; id: number | string } | null;

export type WorldState = {
  map: WorldMap | null;
  buildings: Record<string, import('../world/types').WorldBuilding>; // id → building
  buildMode: BuildMode;
  selectedEntity: SelectedEntity;
  hoveredTile: TilePos | null;
  camera: { x: number; y: number; zoom: number };
  truckVisuals: Record<string, TruckVisual>;
  visitorVisuals: VisitorVisual[];
  mapReady: boolean;

  initMap: (seed?: number) => void;
  setBuildMode: (mode: BuildMode) => void;
  selectEntity: (sel: SelectedEntity) => void;
  setHoveredTile: (tile: TilePos | null) => void;
  setCamera: (cam: { x?: number; y?: number; zoom?: number }) => void;
  addBuilding: (building: import('../world/types').WorldBuilding) => void;
  removeBuilding: (id: string) => void;
};

const useWorldStore = create<WorldState>()(
  immer((set) => ({
    map: null,
    buildings: {},
    buildMode: 'none',
    selectedEntity: null,
    hoveredTile: null,
    camera: { x: 0, y: 0, zoom: 1 },
    truckVisuals: {},
    visitorVisuals: [],
    mapReady: false,

    initMap: (seed) => {
      // Persist the seed so a reload restores the same map (session 6 keeps this).
      let s = seed;
      if (s === undefined) {
        try {
          const stored = localStorage.getItem('bt-map-seed');
          s = stored !== null ? Number(stored) : Math.floor(Math.random() * 0xffffffff);
        } catch {
          s = Math.floor(Math.random() * 0xffffffff);
        }
        if (!Number.isFinite(s)) s = Math.floor(Math.random() * 0xffffffff);
      }
      try {
        localStorage.setItem('bt-map-seed', String(s));
      } catch {
        // private mode / storage blocked — session-only map is fine
      }
      const map = generateMap(s);
      set((draft) => {
        draft.map = map;
        draft.buildings = {};
        draft.buildings['hq'] = { id: 'hq', kind: 'hq', x: map.hq.x, y: map.hq.y };
        draft.buildings['plant'] = { id: 'plant', kind: 'plant', x: map.plant.x, y: map.plant.y };
        // Register the buildings onto tiles so placement/selection see them.
        draft.map.tiles[map.hq.y * map.width + map.hq.x].buildingId = 'hq';
        draft.map.tiles[map.plant.y * map.width + map.plant.x].buildingId = 'plant';
        draft.mapReady = true;
        // Center the camera on the HQ tile.
        draft.camera = { x: 0, y: 0, zoom: 1 };
      });
    },

    setBuildMode: (mode) =>
      set((draft) => {
        draft.buildMode = mode;
      }),

    selectEntity: (sel) =>
      set((draft) => {
        draft.selectedEntity = sel;
      }),

    setHoveredTile: (tile) =>
      set((draft) => {
        draft.hoveredTile = tile;
      }),

    setCamera: (cam) =>
      set((draft) => {
        if (cam.x !== undefined) draft.camera.x = cam.x;
        if (cam.y !== undefined) draft.camera.y = cam.y;
        if (cam.zoom !== undefined) draft.camera.zoom = cam.zoom;
      }),

    addBuilding: (building) =>
      set((draft) => {
        draft.buildings[building.id] = building;
        if (draft.map) {
          draft.map.tiles[building.y * draft.map.width + building.x].buildingId = building.id;
        }
      }),

    removeBuilding: (id) =>
      set((draft) => {
        const b = draft.buildings[id];
        if (b && draft.map) {
          draft.map.tiles[b.y * draft.map.width + b.x].buildingId = null;
        }
        delete draft.buildings[id];
      }),
  })),
);

export default useWorldStore;