# Bottle Tycoon — Isometric Game Roadmap

Approved plan (see session history for full detail). Renderer: **PixiJS v8** · backend: **the 5 microservices stay authoritative**; frontend simulates the world and syncs · art: **programmatic vector graphics** (emerald/dark theme, no assets) · mode: **open-ended sandbox**, total earnings = score.

## Architecture

```
React HUD (daisyUI overlays) ←→ useGameStore (existing economy store, untouched) ←→ backend (5001–5005)
          ↕ selection/build commands                    ↕ status/cargo/fill sync
    useWorldStore (new) ←→ WorldEngine (PixiJS singleton, ticker @60fps)
```

- No pixi-react — manual `Application` init in `game/WorldEngine.ts`, mounted by `components/world/GameCanvas.tsx`.
- Two clocks: existing `setInterval` economy loops (`hooks/useGameLoop.ts`) + Pixi ticker for trucks/camera/visitors reading `useWorldStore.getState()` non-reactively.
- `store/worldBridge.ts` maps economy events → world actions and **replaces the fixed `setTimeout(deliverToPlant, 10000/mult)` with visual-arrival callbacks** (plant POST fires when the truck visually arrives).
- Placement persists via `POST /recyclers` body `location: "x,y"` (Recycler.Location is a passthrough nullable string — verified in `CreateRecyclerEndpoint.cs`); reload restores by parsing `Location`. Map seed in localStorage keyed by playerId. **No backend changes required.**

## Layout

```
src/Frontend/src/
  world/        # pure TS → Vitest: types, rng, mapgen, roadGraph, iso, placement, truckSim
  game/         # PixiJS: WorldEngine, WorldRenderer, draw/{tiles,buildings,truckSprite,overlays}
  components/world/  # GameCanvas, BuildToolbar, EntityInspector
  store/        # useWorldStore, worldBridge
```

## Map generation (40×40, seeded)

Water/forest random-walk blobs → 3 jittered avenue rows + 3 columns (connected ladder) → 6–10 branch random-walks → HQ road-adjacent near center, plant ≥12 BFS-road-distance away → invariants: full road connectivity from HQ stop, ≥10 valid placement tiles, self-reseed on degenerate output.

## Conventions

- Iso tiles 64×32; `toScreen(x,y) = ((x−y)·32, (x+y)·16)` + inverse in `world/iso.ts`.
- Trucks: BFS over road tiles, 2 tiles/s × time multiplier `{1:0, 2:1, 3:2, 4:4, 5:5}` ± 15% per-leg jitter; lifecycle maps to existing statuses (`idle/en route/loading/to_plant/...`).
- Buildings occupy one tile; **truck stop = adjacent road tile**; recycler placement requires road adjacency (`world/placement.ts`).
- HUD: Header unchanged; canvas full-bleed with daisyUI overlays (BuildToolbar top-left, EntityInspector right, ActivityLog bottom-left, EarningsChart bottom-right, both collapsible). Old card grid demoted to collapsible "Classic panels" drawer in session 5.
- TDD: world/ modules are Vitest-covered (`npm run test:unit`); never hand-edit package-lock.json.

## Sessions

| # | Scope | Done when |
|---|---|---|
| 1 | **World domain core**: `world/` modules + Vitest, app unchanged | ✅ this session — 40 tests green, tsc + vite build clean |
| 2 | Static iso world: pixi.js dep, WorldEngine/Renderer, draw/tiles+buildings, GameCanvas, useWorldStore, pan/zoom | ✅ dev shows pannable map with roads/water/trees/HQ/plant; existing e2e green |
| 3 | Placement: BuildToolbar, EntityInspector, overlays, `startPlacement` → tile click → `POST /recyclers {location}` | ✅ placement vs live services works; reload restores; credits deducted once |
| 4 | Trucks: truckSprite, truckSim integration, worldBridge; dispatch → BFS drive → load → plant → deliver on arrival | ✅ full dispatch→delivery cycle visible; earnings in Header/chart |
| 5 | World life + HUD flip: visitors, in-world bars, plant anim, canvas-primary layout | ✅ manual playthrough; e2e green |
| 6 | Economy/polish: distance-based operatingCost in `/deliveries`, placement UX, DebugPanel world controls, restore | ✅ cost tests; reload restores map + camera |
| 7 | Hardening: perf (60fps @ 10 trucks + 10 recyclers), service-down grace, `tests/e2e/world.spec.ts` | ✅ 120fps @ 10 trucks + 20 visitors; service-down logs errors, world stays alive; full suite green |

## Gotchas

1. No StrictMode in App.tsx today — guard `WorldEngine.init` if one is added; strict `app.destroy(true)` cleanup.
2. Never subscribe React components to per-frame world state (re-render storms); HUD reads coarse state only.
3. Non-2xx from `POST /recyclers` = no building (rollback visual); backend debits — never optimistically deduct.
4. Keep `attemptSmartDispatch` economy logic untouched; visuals wrap it so telemetry payloads stay identical.
5. Redraw `Graphics` only on state change; trucks reposition `Container`s. `resolution: devicePixelRatio` + `autoDensity` for Windows DPI.