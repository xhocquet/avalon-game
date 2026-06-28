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

- `KlothoComponent`: 100-112 used (110 = `UnitMoveTarget`, 111 = `AttackTargetUnitId`, 112 = `PendingRespawn`), next free 113.
- `KlothoSerializable`: 100 `MoveCommand`, 101 `GameOverEvent`, 102 `UnitDiedEvent`, 103 `AttackCommand`, 104 `PlayerDiedEvent`, 105 `PlayerRespawnedEvent`, next free 106.
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
- `CommandSystem` handles `AttackCommand`, validates owned source units and enemy live targets by stable `UnitId`, and writes `AttackTargetUnitId`.
- `AttackTargetUnitId` (`KlothoComponent(111)`) stores command-facing attack intent without exposing transient ECS entity ids.
- `AttackIntentSystem` resolves attack targets each tick, chases moving targets with `UnitMoveTarget`, clears invalid intent, and can reacquire a nearby enemy when the target dies.
- `AttackCooldownSystem` and `DamageSystem` apply deterministic cooldown-gated damage for in-range attack targets.
- `UnitDiedEvent` (`KlothoSerializable(102)`) exists as a synced death event.
- `DeathSystem` removes dead units and raises `UnitDiedEvent`.
- Player heroes spawn with `Health`, `Combat`, and `NavAgentComponent`.
- `RespawnSystem` owns hero death: zero-HP players enter `PendingRespawn`, active movement/attack/nav state is scrubbed while dead, and heroes respawn after a 5-second tick delay.
- `PlayerDiedEvent` and `PlayerRespawnedEvent` exist as synced lifecycle events for client/UI reactions.
- Client right-click ground issues `MoveCommand` for the local selected `UnitId`s; right-click enemy issues `AttackCommand` for selected source `UnitId`s and target `UnitId`.
- Navigation runtime exists: `FPNavMesh` loading, query/pathfinder/funnel, `FPNavAgentSystem`, and `NavigationAgentSystem` integration.
- `NavigationRegion3D.NavMeshData.bytes` lives beside `MapLayout.bytes` under `client/Sim/Data/`; client loads it directly and server/tests copy it from that folder.
- Client, server, and sim tests pass `NavigationRuntime.FromBytes(...)` into `SimulationSetup.RegisterSystems(...)` before world initialization.
- Command-driven `UnitMoveTarget` movement routes through `NavigationAgentSystem` for nav agents; direct transform integration is only the no-nav fallback.
- `FPNavAvoidance` is instantiated and assigned to `FPNavAgentSystem`; tuning/profiling is still pending.
- Heroes and minions are initialized with `NavAgentComponent`; tests cover both.
- `RespawnSystem` resets nav agents when a dead hero respawns.
- `CombatMovementPipelineSystems.cs` still contains the intended pipeline stage names, but most of those stage classes are stubs; the live attack/combat slice currently lives in `AttackIntentSystem`, `AttackCooldownSystem`, and `DamageSystem`.
- `MinionMoveSystem` is no longer registered by normal sim setup, but the legacy file still exists and should be deleted once movement ownership is fully settled.

## Next Slice: Autonomous Combat Acquisition

Goal: make units acquire nearby enemies deterministically so minions can meet, fight, and die without explicit player attack commands.

1. Add autonomous deterministic nearest-enemy acquisition using `Team`, `Unit`, `TransformComponent`, `Health`, and `Combat`.
2. Use stable targeting priority: hero/champion -> minion -> structure, then distance, then `UnitId`.
3. Feed acquired targets through the existing `AttackTargetUnitId`, `AttackIntentSystem`, cooldown, damage, and death path.
4. Add focused tests for acquisition priority and autonomous target switching.

Acceptance:

- Opposing minions can acquire targets without player commands.
- Damage and death remain deterministic and synced.
- Explicit player attack orders still override autonomous acquisition.

## Milestone A: Combat And Death

Goal: make minions meet, fight, die

1. Move or fold the current live attack/combat behavior into the intended pipeline stages when the stage boundaries are actually useful; do not do a broad rewrite just for naming.
2. Keep `sim/Systems/DeprecatedCombatSystem.cs` as reference only; do not re-enable it wholesale.
3. Add autonomous deterministic nearest-enemy acquisition using `Team`, `Unit`, `TransformComponent`, `Health`, and `Combat`.
4. Use stable targeting priority: hero/champion -> minion -> structure, then distance, then `UnitId`.
5. Keep attacks applying damage through cooldown-gated combat systems, then let `DeathSystem` remove non-player units and raise `UnitDiedEvent`.
6. Add focused tests for acquisition priority and autonomous target switching; cooldown timing, direct attack damage, and death already have coverage.
7. View reacts to synced attack/death events for VFX only.

Acceptance:

- Opposing minions damage each other and die in sync.
- Death removes entities from the deterministic sim.
- High-count waves do not rely on physics bodies or Godot overlap triggers.

## Milestone B: Navigation

Goal: replace straight-line marching with deterministic A* pathing so minions route around structures and the map has meaningful shape.

Navmesh is needed now, not later: without it, minions pile at the center regardless of map geometry, and turrets/structures have no spatial meaning.

Runtime nav loading is wired. The remaining gap is deciding which sim systems own autonomous movement requests and how they interact with combat pursuit.

1. Keep command-driven and combat-pursuit movement flowing through `UnitMoveTarget` so `NavigationAgentSystem` owns nav-agent movement.
2. Delete the legacy `MinionMoveSystem` file once no normal movement path depends on it.
3. Use `NavAgentComponent.Stop()` / `SetDestination()` for combat interruption and resume where needed.
4. Add focused tests for nav-owned movement without direct transform integration.
5. Verify structure/turret footprints are actually absent from the baked navmesh.
6. Tune or gate `FPNavAvoidance` (ORCA) after combat creates enough crowding to justify it; the runtime hook is already enabled.

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

1. Client already keeps selection locally and renders selection indicators; keep that view-only. Done.
2. Right-click ground sends `MoveCommand` with explicit bounded `UnitId` list. Done.
3. Right-click enemy sends `AttackCommand` with selected source `UnitId`s and target `UnitId`. Done.
4. SIM validates ownership and applies orders. Done.
5. WASD free-camera stays as a permanent debug/spectator tool; it is not a gameplay command.

Acceptance:

- Right-click ground issues a `MoveCommand` for selected units by `UnitId`.
- Right-click enemy issues `AttackCommand` by target `UnitId`.
- Selection does not appear in the command recording.

## Milestone E: Avoidance And Scale

Goal: scale toward hundreds or thousands of units without physics. Nav paths are handled by `FPNavAgentSystem`; this milestone is about agent separation and iteration cost.

1. Tune, profile, or conditionally gate `FPNavAvoidance` (ORCA); it is already wired into `NavigationRuntime`.
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

## Todo, No Particular Order

- Add a bounded async logging sink for server diagnostics. Sim ticks must not block on console/file writes; enqueue logs to a background writer, drop or coalesce low-priority lines when the queue is full, and flush only on shutdown or bounded intervals.
- Add a dynamic view/object pool for minions; a fixed 64-object pool is probably too small once waves stack up.
