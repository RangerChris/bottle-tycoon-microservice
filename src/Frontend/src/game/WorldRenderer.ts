// Renders the world map into layer containers. Terrain/roads are drawn once
// per map; buildings redraw when the set changes. Units (trucks/visitors)
// come in session 4/5.
import { Container } from 'pixi.js';
import type { WorldMap } from '../world/types';
import { drawWorldTiles, type TileLayers } from './draw/tiles';
import { buildingContainer } from './draw/buildings';

export type WorldLayers = {
  root: Container; // the camera-panned/zoomed container
  terrain: Container;
  roads: Container;
  buildings: Container;
  units: Container;
  status: Container; // capacity bars above buildings
  fx: Container; // transient effects (plant unload pulse)
  overlays: Container;
};

export function createLayers(): WorldLayers {
  const root = new Container();
  const terrain = new Container();
  const roads = new Container();
  const buildings = new Container();
  const units = new Container();
  const status = new Container();
  const fx = new Container();
  const overlays = new Container();
  buildings.sortableChildren = true; // zIndex = x + y
  root.addChild(terrain, roads, buildings, units, status, fx, overlays);
  return { root, terrain, roads, buildings, units, status, fx, overlays };
}

// Full redraw (called once on map init; later sessions add partial redraws).
// Buildings are NOT drawn here — syncBuildings owns building sprites.
export function renderMap(map: WorldMap, layers: WorldLayers): void {
  drawWorldTiles(map, { terrain: layers.terrain, roads: layers.roads });
}

// Adds (or re-adds) a single building container at its tile.
export function addBuildingSprite(layers: WorldLayers, kind: 'hq' | 'plant' | 'recycler', x: number, y: number, id: string): void {
  // Replace any existing sprite for this id.
  removeBuildingSprite(layers, id);
  const c = buildingContainer(kind, x, y);
  (c as unknown as { label: string }).label = id; // pixi v8 containers carry a label
  layers.buildings.addChild(c);
}

export function removeBuildingSprite(layers: WorldLayers, id: string): void {
  const existing = layers.buildings.children.find((c) => (c as unknown as { label?: string }).label === id);
  if (existing) {
    existing.destroy({ children: true });
  }
}