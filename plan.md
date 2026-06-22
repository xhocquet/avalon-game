# Avalon Simulation Plan

Target shape: Warcraft/Dota-like top-down combat with a handful of human players and large deterministic armies. The server is authoritative. Clients send commands, predict locally, then reconcile against server-verified state.

## Current Model

- `sim/**` (repo root) is shared deterministic gameplay code compiled by both client and server. Data assets live at `client/Sim/Data/` (Godot `res://` requires them inside the Godot project).
- Godot nodes are view/input only. They do not own authoritative gameplay state.
- Klotho ServerDriven mode is not classic wait-for-all lockstep. The server advances fixed ticks, substitutes empty input when needed, broadcasts verified state, and clients rollback/reconcile.
- Keep network and replay data command-centric. Commands are the durable record of player intent.
- Keep sim state light. Do not network or store per-frame unit transforms as the gameplay protocol.
- Movement is planar. `TransformComponent.Position.x/z` is authoritative; `y` is not gameplay.
- Avoid dynamic physics bodies for units. Use deterministic transform integration, radii, proximity queries, grids, and stable iteration order.

## Identity And Command Rules

- Use stable `Unit.UnitId` for command references, events, selection, targeting, replay, and UI bookkeeping.
- Do not put transient ECS entity ids in command payloads.
- Validate every command in SIM:
  - `PlayerId` must own or be allowed to command the referenced unit.
  - referenced `UnitId`s must still exist at execution time.
  - stale commands should no-op deterministically, not throw.
- Prefer compact intent commands:
  - `SelectCommand { selection input shape }`
  - `MoveToCommand { target position }`
  - `AttackCommand { target UnitId }`
  - `Spawn/Buy/TrainCommand { unit type, source structure UnitId }`
  - `AbilityCommand { caster UnitId, ability id, target UnitId/position }`
- Selection is client/view state for now. The command stream sends explicit `UnitId` targets for the current local selection.
- Keep the design open to SIM-owned selection later by using stable `Unit.UnitId` references and deterministic validation helpers.
- Group orders may carry explicit `UnitId`s for now, but keep payloads bounded and avoid making large per-frame selection lists part of the normal protocol.
- Commands should carry enough intent to reproduce behavior, not sampled movement state.

## Klotho Ids

- `KlothoComponent`: 100-110 used (110 = `UnitMoveTarget`), next free 111.
- `KlothoSerializable`: 100 `MoveCommand`, 101 `GameOverEvent`, 102 reserved for `UnitDiedEvent`, 103 `AttackCommand`, next free 104.
- `KlothoDataAsset`: 100 `PlayerStats`, 101 `WaveRules`, 102 `MapLayout`, next free 103.
- Note: `NavAgentComponent` uses Klotho-internal ID 11 — no conflict with project range.

## Done

- ServerDriven client/server flow is wired.
- Shared sim bootstrap creates bases, spawn points, heroes, teams, health, and stable unit ids.
- Minion waves exist through `WaveRulesAsset` and `WaveSpawnSystem`.
- Minion view exists through `UnitViewFactory`.
- Minions move deterministically toward center with transform-only movement.
- Player movement no longer uses `PhysicsBodyComponent`; it directly integrates `TransformComponent.Position.x/z`.
- Klotho physics is no longer registered for core gameplay movement.
- `UnitIdGenerator` provides stable sim-level unit identity.
- `SimMarkerNode` ([Tool][GlobalClass] Node3D) places Base/SpawnPoint/Shop/Turret markers in the editor.
- `MapLayoutAsset` (KlothoDataAsset 102) stores marker positions; `GodotFPMapLayoutExporter` bakes them to `Sim/Data/MapLayout.bytes`.
- `SimulationSetup` uses `MapLayoutAsset` for base/spawn positions when available, falls back to hardcoded corners.
- Map layout foundation is complete: editor-authored Base/SpawnPoint/Shop/Turret markers can drive baked layout data for sim spawn/base placement.

## Next Slice: Command And Unit Identity Foundation

Goal: make commands ready for real unit orders before adding deeper combat.

1. Add shared helpers for resolving `UnitId -> entity`.
2. Add ownership/team validation helpers for command systems.
3. Add `MoveToCommand`: carries an explicit list of `UnitId`s (client-side selection is view-only and not recorded). Applies movement to owned units.
4. Add `AttackCommand { caster UnitId, target UnitId }`.
5. Selection is client-side UI state only for now — not SIM state, not in the command stream, not in recordings.

Acceptance:

- A command can reference units by `UnitId`.
- SIM can deterministically resolve, validate, and no-op stale unit references.
- No command depends on Godot node paths or transient ECS entity ids.

## Milestone A: Combat And Death

Goal: make minions meet, fight, die, and reduce live entity counts deterministically.

1. `UnitDiedEvent` (`KlothoSerializable(102)`): `{ UnitId, UnitTypeId, Position }`.
2. `DeathSystem`: remove entities with `Health.Current <= 0`; raise `UnitDiedEvent`.
3. `CombatSystem`: deterministic nearest-enemy acquisition using `Team`, `Unit`, `TransformComponent`, `Health`, and `Combat`.
4. Stable targeting priority: hero/champion → minion → structure.
5. View reacts to synced attack/death events for VFX only.

Acceptance:

- Opposing minions damage each other and die in sync.
- Death removes entities from the deterministic sim.
- High-count waves do not rely on physics bodies or Godot overlap triggers.

## Milestone B: Navigation

Goal: replace straight-line marching with deterministic A* pathing so minions route around structures and the map has meaningful shape.

Navmesh is needed now, not later: without it, minions pile at the center regardless of map geometry, and turrets/structures have no spatial meaning.

1. Bake `NavigationRegion3D` in Godot using the Klotho plugin's `GodotFPNavMeshExporter`. Structure `StaticBody3D` nodes block the bake automatically.
2. Load `FPNavMesh` from the exported `.bytes` file in `SimulationSetup` before `InitializeWorld()`.
3. Instantiate `FPNavMeshQuery`, `FPNavMeshPathfinder`, `FPNavMeshFunnel`, `FPNavAgentSystem`.
4. Add `NavAgentComponent` to minions and heroes at spawn; call `NavAgentComponent.SetDestination()` with enemy base position.
5. Replace `MinionMoveSystem` straight-line integration with `FPNavAgentSystem.Update()` + write `nav.Position` back to `TransformComponent`.
6. `NavAgentComponent.Stop()` / `SetDestination()` is the hook for combat interruption and resume.
7. Wire `FPNavAvoidance` (ORCA) for minion separation if counts justify it.

Acceptance:

- Minions path around structure footprints deterministically.
- Commands remain intent-only (target position or `UnitId`), not path samples.
- All peers derive identical paths from the same navmesh and start position.

## Milestone C: Structures And Win Condition

Goal: first playable deterministic Footmen-Frenzy slice. Nav is a prerequisite so turrets have spatial meaning.

1. Bases already spawn with `Health`; make base death emit `GameOverEvent`.
2. Add turret units: stationary, have `Combat` component, are targeted by `CombatSystem`, attack enemies in range.
3. Turrets are nav obstacles at bake time (their `StaticBody3D` blocks the navmesh).
4. Add simple structure views and team tinting.

Acceptance:

- Minions path through the map, encounter turrets, fight them, and eventually reach and destroy a base.
- Server and clients agree on winner through synced deterministic state.

## Milestone D: Click Orders

Goal: replace direct WASD hero movement with command-based MOBA control.

1. Client raycasts mouse input into deterministic planar world coordinates.
2. Client maintains selection locally (view-only); sends `MoveToCommand` / `AttackCommand` with explicit bounded `UnitId` list.
3. SIM validates ownership and applies orders.
4. View renders selection indicators from local client state.
5. WASD free-camera stays as a permanent debug/spectator tool; it is not a gameplay command.

Acceptance:

- Right-click ground issues a `MoveToCommand` for selected units by `UnitId`.
- Right-click enemy issues `AttackCommand` by target `UnitId`.
- Selection does not appear in the command recording.

## Milestone E: Avoidance And Scale

Goal: scale toward hundreds or thousands of units without physics. Nav paths are handled by `FPNavAgentSystem`; this milestone is about agent separation and iteration cost.

1. Enable `FPNavAvoidance` (ORCA) if not already wired in Milestone B.
2. Add spatial grid for proximity scans in `CombatSystem` if needed.
3. Keep all iteration order stable.
4. Profile and tune at target unit counts.

Acceptance:

- Large minion counts remain stable and cheap.
- No dynamic physics bodies are required for normal unit movement.

## Milestone F: HUD, Camera, And Polish

- Health bars, minimap, scoreboard, and match clock are view-only.
- Camera pan/zoom/follow stays client-only.
- VFX and audio are event-driven from synced events, not gameplay authority.

## Open Decisions

1. MapLayout export trigger: manual editor button in Klotho dock, or auto-export on scene save via `@tool`.

## Todo, No Particular Order

- Add a dynamic view/object pool for minions; a fixed 64-object pool is probably too small once waves stack up.
