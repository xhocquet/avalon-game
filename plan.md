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
  - `MoveCommand { target position }`
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
- `KlothoDataAsset`: 100 `PlayerStats`, 101 `WaveRules`, 102 `MapLayout`, 103 `MinionStats`, next free 104.
- Note: `NavAgentComponent` uses Klotho-internal ID 11 — no conflict with project range.

## Done

- ServerDriven client/server flow is wired.
- Shared sim bootstrap creates bases, spawn points, heroes, teams, health, and stable unit ids.
- `UnitLookup` provides shared SIM helpers for resolving `UnitId -> entity` and player/team ownership validation.
- Command tests cover stale or destroyed `UnitId` lookup behavior.
- Minion waves exist through `WaveRulesAsset` and `WaveSpawnSystem`.
- Minion view exists through `UnitViewFactory`.
- Minions spawn with `NavAgentComponent` and deterministic IDs.
- Player movement no longer uses `PhysicsBodyComponent`; it directly integrates `TransformComponent.Position.x/z`.
- `MoveCommand` carries explicit selected `UnitId`s and applies movement only to units owned by the issuing player's team.
- Selected move commands can move hero/minion groups together, including simple deterministic formation slots.
- Selection is client-side UI state only for now: not SIM state, not in the command stream, and not in recordings.
- Client selection supports single-select, drag-select, selection indicators, and fallback focus on the local player.
- Klotho physics is no longer registered for core gameplay movement.
- `UnitIdGenerator` provides stable sim-level unit identity.
- `SimMarkerNode` ([Tool][GlobalClass] Node3D) places Base/SpawnPoint/Shop/Turret markers in the editor.
- `MapLayoutAsset` (KlothoDataAsset 102) stores marker positions; `GodotFPMapLayoutExporter` bakes them to `Sim/Data/MapLayout.bytes`.
- `SimulationSetup` requires `MapLayoutAsset` for base/spawn positions and fails loudly when markers are missing.
- Map layout foundation is complete: editor-authored Base/SpawnPoint/Shop/Turret markers can drive baked layout data for sim spawn/base placement.
- Client and server load `MapLayout.bytes` as runtime data.
- `AttackCommand` (`KlothoSerializable(103)`) exists and serializes target/source `UnitId`s.
- `UnitDiedEvent` (`KlothoSerializable(102)`) exists as a synced death event.
- `DeathSystem` removes dead units and raises `UnitDiedEvent`.
- Navigation runtime exists: `FPNavMesh` loading, query/pathfinder/funnel, `FPNavAgentSystem`, and `NavigationAgentSystem` integration.
- Heroes and minions are initialized with `NavAgentComponent`; tests cover both.
- `RespawnSystem` resets nav agents when a falling hero respawns.
- `CombatMovementPipelineSystems.cs` registers the intended combat/movement pipeline names, but most of those systems are still stubs.

## Next Slice: Explicit Attack Orders

Goal: make right-click attack intent executable in SIM before filling in autonomous combat.

1. Add `AttackCommand` handling in `CommandSystem`.
2. Validate source units with `UnitLookup.TryGetPlayerOwnedUnitById`.
3. Resolve target by `UnitId`; no-op if the target is missing, dead, or same-team.
4. Store attack intent with stable `UnitId` data, not transient ECS entity ids. If `Combat.Target` stays as `EntityRef`, add a separate command-facing target component or resolve every tick from `UnitId`.
5. Stop or replace any existing `UnitMoveTarget` when an attack order is accepted.
6. Add command tests for:
   - missing target no-ops;
   - destroyed target no-ops;
   - non-owned source no-ops;
   - same-team target no-ops;
   - valid source/target records attack intent.

Acceptance:

- `AttackCommand` affects only owned source units.
- Stale or invalid `UnitId` references no-op deterministically.
- No command stores Godot node paths or relies on externally visible ECS entity ids.

## Milestone A: Combat And Death

Goal: make minions meet, fight, die

1. Replace `CombatMovementPipelineSystems.cs` stubs with the smallest real combat loop.
2. Keep `sim/Systems/DeprecatedCombatSystem.cs` as reference only; do not re-enable it wholesale.
3. Add deterministic nearest-enemy acquisition using `Team`, `Unit`, `TransformComponent`, `Health`, and `Combat`.
4. Use stable targeting priority: hero/champion -> minion -> structure, then distance, then `UnitId`.
5. Make attacks apply damage through the new pipeline, then let `DeathSystem` remove units and raise `UnitDiedEvent`.
6. Add focused tests for acquisition priority, cooldown timing, damage, and death.
7. View reacts to synced attack/death events for VFX only.

Acceptance:

- Opposing minions damage each other and die in sync.
- Death removes entities from the deterministic sim.
- High-count waves do not rely on physics bodies or Godot overlap triggers.

## Milestone B: Navigation

Goal: replace straight-line marching with deterministic A* pathing so minions route around structures and the map has meaningful shape.

Navmesh is needed now, not later: without it, minions pile at the center regardless of map geometry, and turrets/structures have no spatial meaning.

1. Add the actual navmesh bytes to runtime loading if not already exported with the current map.
2. Verify `NavigationRuntime.FromBytes(...)` is used by both client and server startup before `SimulationSetup.InitializeWorld(...)`.
3. Set default minion lane goals to enemy base positions through `UnitMoveTarget` so `NavigationAgentSystem` owns movement.
4. Delete or fully retire `MinionMoveSystem` once all minion movement flows through nav agents.
5. Use `NavAgentComponent.Stop()` / `SetDestination()` for combat interruption and resume.
6. Wire `FPNavAvoidance` (ORCA) only after combat creates enough crowding to justify it.

Acceptance:

- Minions path around structure footprints deterministically.
- Commands remain intent-only (target position or `UnitId`), not path samples.
- All peers derive identical paths from the same navmesh and start position.

## Milestone C: Structures And Win Condition

Goal: first playable deterministic Footmen-Frenzy slice. Nav is a prerequisite so turrets have spatial meaning.

1. Bases already spawn with `Health`; make base death emit `GameOverEvent` instead of only relying on match timeout.
2. Add turret units: stationary, have `Combat` component, are targeted by the combat pipeline, attack enemies in range.
3. Turrets are nav obstacles at bake time (their `StaticBody3D` blocks the navmesh).
4. Add simple structure views and team tinting.

Acceptance:

- Minions path through the map, encounter turrets, fight them, and eventually reach and destroy a base.
- Server and clients agree on winner through synced deterministic state.

## Milestone D: Click Orders

Goal: replace direct WASD hero movement with command-based MOBA control.

1. Client already keeps selection locally and renders selection indicators; keep that view-only.
2. Right-click ground sends `MoveCommand` with explicit bounded `UnitId` list.
3. Right-click enemy sends `AttackCommand` with selected source `UnitId`s and target `UnitId`.
4. SIM validates ownership and applies orders.
5. WASD free-camera stays as a permanent debug/spectator tool; it is not a gameplay command.

Acceptance:

- Right-click ground issues a `MoveCommand` for selected units by `UnitId`.
- Right-click enemy issues `AttackCommand` by target `UnitId`.
- Selection does not appear in the command recording.

## Milestone E: Avoidance And Scale

Goal: scale toward hundreds or thousands of units without physics. Nav paths are handled by `FPNavAgentSystem`; this milestone is about agent separation and iteration cost.

1. Enable `FPNavAvoidance` (ORCA) if not already wired in Milestone B.
2. Add spatial grid for proximity scans in the combat pipeline if needed.
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
2. Attack intent storage: add a stable `AttackTargetUnitId` component, or keep `Combat.Target` internal and derive it from command/acquisition state each tick.
3. Navigation bytes ownership: decide whether navmesh data lives beside `MapLayout.bytes` under `client/Sim/Data/` and is copied to server output the same way.

## Todo, No Particular Order

- Add a dynamic view/object pool for minions; a fixed 64-object pool is probably too small once waves stack up.
