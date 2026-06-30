# Avalon

- `sim/` is the deterministic sim used by both client and server.
- Data lives at `client/Sim/Data/` (Godot `res://` requires them inside the Godot project).
- The server advances fixed ticks, substitutes empty input when needed, broadcasts verified state, and clients rollback/reconcile.
- The only inputs to the sim are commands and time. Those get shared to clients.
- Commands use fixed, deterministic IDs. All commands are validated server side before being broadcast.
- Only X+Z. No vertical movement/physics.
- No built-in physics. Use deterministic transform integration, radii, proximity queries, grids, and stable iteration order.
- Client selection not included in the sim
- Godot is a view and input layer.


## Network Ids

### `KlothoComponent`

| ID  | Name                 | Notes                                                                                                             |
| --- | -------------------- | ----------------------------------------------------------------------------------------------------------------- |
| 100 | `Player`             | Human or computer participant state; stores player identity/score and can later tie to an external account.       |
| 101 | `Unit`               | Stable `UnitId` references for commands, events, selection, targeting, ownership validation, and lookup behavior. |
| 102 | `Team`               | Team ownership and enemy validation.                                                                              |
| 103 | `Health`             | Shared HP for heroes, minions, crystals, and turrets.                                                             |
| 104 | `Hero`               | Main controllable character for a human or computer player.                                                       |
| 105 | `Minion`             | Wave-spawned unit that can be commanded by the same human or computer users.                                      |
| 106 | `Crystal`            | Team structure marker.                                                                                            |
| 107 | `SpawnPoint`         | Team spawn and wave source marker.                                                                                |
| 108 | `Combat`             | Damage, range, cooldown, and transient target state.                                                              |
| 109 | `UnitIdCounter`      | Singleton stable unit-id generator.                                                                               |
| 110 | `UnitMoveTarget`     | Command/combat movement intent consumed by `NavigationAgentSystem` for nav-agent movement.                        |
| 111 | `AttackTargetUnitId` | Command-facing attack intent; stores target `UnitId` without exposing transient ECS entity ids.                   |
| 112 | `PendingRespawn`     | Dead hero respawn countdown and command/nav scrub marker.                                                         |
| 113 | `Turret`             | Stationary combat structure marker.                                                                               |

### `KlothoSerializable`

| ID  | Name                    | Notes                                                                                                                                                                                        |
| --- | ----------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 100 | `MoveCommand`           | Right-click ground order; carries explicit bounded selected `UnitId`s and target position, supports hero/minion formation slots, and applies only to units owned by the issuing player team. |
| 101 | `GameOverEvent`         | Synced match-end event and winner payload.                                                                                                                                                   |
| 102 | `UnitDiedEvent`         | Synced non-player unit death event.                                                                                                                                                          |
| 103 | `AttackCommand`         | Right-click enemy order; carries source `UnitId`s and target `UnitId`; `CommandSystem` validates owned sources and live enemy targets.                                                       |
| 104 | `PlayerDiedEvent`       | Synced hero death lifecycle event.                                                                                                                                                           |
| 105 | `PlayerRespawnedEvent`  | Synced hero respawn lifecycle event.                                                                                                                                                         |
| 106 | `CrystalDestroyedEvent` | Synced crystal destruction event; win condition evaluation may consume it but crystal loss is not automatically a match end.                                                                 |
| 107 | `TurretDestroyedEvent`  | Planned synced turret destruction event.                                                                                                                                                     |

### `KlothoDataAsset`

| ID  | Name          | Notes                                                                                   |
| --- | ------------- | --------------------------------------------------------------------------------------- |
| 100 | `PlayerStats` | Hero movement/health tuning; existing asset name, not account/player-participant state. |
| 101 | `WaveRules`   | Wave timing/count and spawn spacing.                                                    |
| 102 | `MapLayout`   | Baked Crystal/SpawnPoint/Shop/Turret marker positions.                                  |
| 103 | `MinionStats` | Minion health/movement/combat tuning.                                                   |

### Klotho Internal

| ID  | Name                | Notes                                                          |
| --- | ------------------- | -------------------------------------------------------------- |
| 11  | `NavAgentComponent` | Packaged Klotho nav component; no conflict with project range. |

Next free project IDs: `KlothoComponent` 114, `KlothoSerializable` 108, `KlothoDataAsset` 104.

## Systems

| System                    | Area       | Notes                                                                                                                   |
| ------------------------- | ---------- | ----------------------------------------------------------------------------------------------------------------------- |
| `CommandSystem`           | Commands   | Validates and applies `MoveCommand` and `AttackCommand`; writes `UnitMoveTarget` and `AttackTargetUnitId`.              |
| `WaveSpawnSystem`         | Spawning   | Spawns deterministic team minion waves from `SpawnPoint` markers using wave/minion data assets.                         |
| `TargetAcquisitionSystem` | Combat     | Gives eligible combat units autonomous enemy acquisition when they have no attack target or move target.                |
| `AttackIntentSystem`      | Combat     | Resolves attack targets each tick, chases moving targets, and clears invalid intent when targets die or become invalid. |
| `AttackCooldownSystem`    | Combat     | Decrements attack cooldowns.                                                                                            |
| `DamageSystem`            | Combat     | Applies deterministic cooldown-gated damage for in-range attack targets.                                                |
| `DeathSystem`             | Lifecycle  | Removes dead non-player units and raises unit, crystal, or turret death/destruction events.                             |
| `RespawnSystem`           | Lifecycle  | Owns player death, scrubs active state during `PendingRespawn`, respawns after 5 seconds, and resets nav agents.        |
| `NavigationAgentSystem`   | Navigation | Consumes `UnitMoveTarget`, runs `NavigationRuntime.AgentSystem`, and syncs `NavAgentComponent` back to transforms.      |
| `ScoreSystem`             | Match      | Raises payload-free `GameOverEvent` on match timeout; structure win-condition consumption remains.                      |
| `EventSystem`             | Lifecycle  | Klotho runtime system that dispatches raised simulation events.                                                         |
| `FPNavAgentSystem`        | Navigation | Runtime helper owned by `NavigationRuntime`; `NavigationAgentSystem` calls it when nav bytes are loaded.                |
| `FPNavAvoidance`          | Navigation | ORCA avoidance helper instantiated by `NavigationRuntime` and assigned to `FPNavAgentSystem`.                           |

## Shared Client Server

- `SimMarkerNode` ([Tool][GlobalClass] Node3D) places sim layout markers in the editor.
- `MapLayoutAsset` (KlothoDataAsset 102) stores marker positions; `GodotFPMapLayoutExporter` bakes them to `Sim/Data/MapLayout.bytes`.
- `SimulationSetup` requires `MapLayoutAsset` for structure/spawn positions and fails loudly when markers are missing.
- Map layout foundation is complete: editor-authored markers can drive baked layout data for sim spawn/structure placement.
- Client and server load `MapLayout.bytes` as runtime data.
- Navigation runtime exists: `FPNavMesh` loading, query/pathfinder/funnel, and sim integration.
- `NavigationRegion3D.NavMeshData.bytes` lives beside `MapLayout.bytes` under `client/Sim/Data/`; client and server load it from that shared data path.
- Client and server register `NavigationRuntime.FromBytes(...)` before world initialization.

## Determinism

- `UnitIdGenerator` provides stable sim-level unit identity.

## Node Types

| Status | Node Type    | Sim State                                                                  | View / Layout                                                         | Notes                                                                                                           |
| ------ | ------------ | -------------------------------------------------------------------------- | --------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------- |
| ✅      | Hero / Champ | `Hero`, `Player`, `Health`, `Combat`, `NavAgentComponent`, stable `UnitId` | Hero/champ view scenes via `UnitViewFactory`                          | Main controllable character; currently carries `Player` participant state and respawns through `RespawnSystem`. |
| ✅      | Minion       | `Minion`, `Health`, `Combat`, `NavAgentComponent`, stable `UnitId`         | Minion waves via `WaveSpawnSystem`; minion view via `UnitViewFactory` | Wave-spawned controllable unit.                                                                                 |
| ✅      | Turret       | `Turret`, `Health`, `Combat`, team ownership, stable `UnitId`              | `MapMarkerType.Turret`; turret view via `UnitViewFactory`             | Stationary combat structure; acquires targets but does not chase.                                               |
| 🟡      | Crystal      | `Crystal`, `Health`, team ownership, stable `UnitId`                       | `MapMarkerType.Crystal`; crystal view via `UnitViewFactory`           | Team core structure; destruction should emit `CrystalDestroyedEvent`.                                           |
| 🟡      | Shop         | `MapMarkerType.Shop` in baked `MapLayoutAsset`; no gameplay component yet  | Existing world-scene shop marker/view                                 | Marker is exported for future game logic.                                                                       |

## Events

| Status | Event                   | Raised by       | Notes                                                                                                                       |
| ------ | ----------------------- | --------------- | --------------------------------------------------------------------------------------------------------------------------- |
| ✅      | `UnitDiedEvent`         | `DeathSystem`   | Synced non-player, non-structure unit death event.                                                                          |
| ✅      | `PlayerDiedEvent`       | `RespawnSystem` | Synced hero death lifecycle event for client/UI reactions.                                                                  |
| ✅      | `PlayerRespawnedEvent`  | `RespawnSystem` | Synced hero respawn lifecycle event for client/UI reactions.                                                                |
| ✅      | `CrystalDestroyedEvent` | `DeathSystem`   | Synced crystal destruction event with destroyed crystal, owner/team, and killer context; does not directly imply game over. |
| ✅      | `TurretDestroyedEvent`  | `DeathSystem`   | Synced turret destruction event with destroyed turret `UnitId` and destroyer `UnitId`.                                      |
| ✅      | `GameOverEvent`         | `ScoreSystem`   | Payload-free synced match-end event; timeout path raises it, structure win condition still needs to consume crystal loss.   |

## UI

| Status | UI           | Source State | Notes                                           |
| ------ | ------------ | ------------ | ----------------------------------------------- |
| ✅      | `HealthBars` | `Health`     | Renders for all live view entities with health. |
| ⬜      | Minimap      | TBD          | Match/map awareness UI remains.                 |


## Next Slice: Crystal Destruction And Win Condition

Goal: emit crystal destruction as its own synced event, then end the match only when sim player/team state says the win condition is met.

| Status | Work                                                                                      |
| ------ | ----------------------------------------------------------------------------------------- |
| ✅      | Detect crystal death before `DeathSystem` destroys the entity.                            |
| ⬜      | Raise `CrystalDestroyedEvent` with the destroyed crystal, owner/team, and killer context. |
| ⬜      | Evaluate player/team state after crystal destruction.                                     |
| ⬜      | Raise `GameOverEvent` only when the evaluated win condition says the match is over.       |
| ⬜      | Prevent double-emits between timeout and crystal-driven game over.                        |

| Status | Acceptance                                                                  |
| ------ | --------------------------------------------------------------------------- |
| ⬜      | Destroying a crystal emits `CrystalDestroyedEvent` deterministically.       |
| ⬜      | Crystal destruction alone does not automatically emit `GameOverEvent`.      |
| ⬜      | Server and clients agree on winner when `GameOverEvent` is emitted.         |
| ⬜      | Timeout scoring still works when no crystal-driven win condition has fired. |

## Milestone A: Combat And Death

Goal: make minions meet, fight, die

| Status | Work                                                                                                          |
| ------ | ------------------------------------------------------------------------------------------------------------- |
| ✅      | Autonomous deterministic enemy acquisition is implemented for minions, heroes, and turrets.                   |
| ✅      | Target priority is minion -> hero, then lowest `UnitId`.                                                      |
| ✅      | Cooldown-gated attacks apply damage, and `DeathSystem` removes dead non-player units through `UnitDiedEvent`. |
| ⬜      | Add client VFX/audio reactions from synced attack/death events.                                               |

| Status | Acceptance                                                                |
| ------ | ------------------------------------------------------------------------- |
| ✅      | Opposing minions damage each other and die in sync.                       |
| ✅      | Death removes entities from the deterministic sim.                        |
| ✅      | High-count waves do not rely on physics bodies or Godot overlap triggers. |

## Milestone B: Navigation

Goal: route ordered minion movement through deterministic A* pathing so commanded or AI-directed units can navigate around structures.

Navmesh is needed now, not later: commanded or future AI-directed units need map-aware routes, and turrets/structures need spatial meaning.

Runtime nav loading is wired. Remaining work is navmesh verification and avoidance profiling.

| Status | Work                                                                                                                                 |
| ------ | ------------------------------------------------------------------------------------------------------------------------------------ |
| ✅      | Keep command-driven and combat-pursuit movement flowing through `UnitMoveTarget` so `NavigationAgentSystem` owns nav-agent movement. |
| ⬜      | Verify structure/turret footprints are absent from the baked navmesh.                                                                |
| ⬜      | Profile `FPNavAvoidance` with crowded waves; keep tuned settings or gate it behind config.                                           |

| Status | Acceptance                                                                           |
| ------ | ------------------------------------------------------------------------------------ |
| 🟡      | Commanded or AI-directed minions path around structure footprints deterministically. |
| ✅      | Commands remain intent-only: target position or `UnitId`, not path samples.          |
| ✅      | All peers derive identical paths from the same navmesh and start position.           |

## Milestone C: Structures And Win Condition

Goal: first playable deterministic Footmen-Frenzy slice. Nav is a prerequisite so turrets have spatial meaning.

| Status | Work                                                                                           |
| ------ | ---------------------------------------------------------------------------------------------- |
| 🟡      | Crystals spawn with `Health`; crystal death still needs to emit `CrystalDestroyedEvent`.       |
| ✅      | Turrets are stationary combat units with acquisition, in-range attacks, and no chase behavior. |
| ⬜      | Verify turrets are nav obstacles at bake time.                                                 |
| 🟡      | Structure views exist for crystals and turrets; team tinting remains.                          |

| Status | Acceptance                                                                                                                            |
| ------ | ------------------------------------------------------------------------------------------------------------------------------------- |
| 🟡      | User-commanded or AI-directed minions can be driven through the map, encounter turrets, fight them, and eventually destroy a crystal. |
| ⬜      | Server and clients agree on winner when player/team state triggers `GameOverEvent`.                                                   |

## Click Orders

| Input                  | Command / State                | Payload                                                  | Sim Handling                                                                                                     |
| ---------------------- | ------------------------------ | -------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------- |
| Local click selection  | Client-only selection state    | Selected view/unit ids are not recorded as sim state.    | No sim mutation.                                                                                                 |
| Right-click ground     | `MoveCommand`                  | Target X/Z plus explicit selected `UnitId`s.             | `CommandSystem` validates ownership and writes `UnitMoveTarget`.                                                 |
| Right-click enemy      | `AttackCommand`                | Target `UnitId` plus explicit selected source `UnitId`s. | `CommandSystem` validates ownership/live enemy target and writes `AttackTargetUnitId` plus initial chase target. |
| WASD / camera controls | Client-only camera/debug input | No gameplay command payload.                             | No sim mutation.                                                                                                 |

## Milestone E: Avoidance And Scale

Goal: scale toward hundreds or thousands of units without physics. Nav paths are handled by `FPNavAgentSystem`; this milestone is about agent separation and iteration cost.

| Status | Work                                                                                                 |
| ------ | ---------------------------------------------------------------------------------------------------- |
| ⬜      | Record a crowded-wave benchmark with `FPNavAvoidance` enabled.                                       |
| ⬜      | If avoidance dominates frame time, add a config gate or cheaper settings and document the threshold. |
| ⬜      | Add a spatial grid for combat proximity scans only if profiling shows a real hotspot.                |
| ✅      | Keep all iteration order stable.                                                                     |

| Status | Acceptance                                                       |
| ------ | ---------------------------------------------------------------- |
| ⬜      | Large minion counts remain stable and cheap.                     |
| ✅      | No dynamic physics bodies are required for normal unit movement. |

## Milestone F: HUD, Camera, And Polish

| Status | Work                                                                       |
| ------ | -------------------------------------------------------------------------- |
| ⬜      | Add minimap UI.                                                            |
| ✅      | Camera pan/zoom/follow stays client-only.                                  |
| ⬜      | VFX and audio are event-driven from synced events, not gameplay authority. |

## Todo, No Particular Order

| Status | Work                                                                                                          |
| ------ | ------------------------------------------------------------------------------------------------------------- |
| ⬜      | Add a bounded async logging sink for server diagnostics.                                                      |
| ⬜      | Add a dynamic view/object pool for minions; a fixed 64-object pool is probably too small once waves stack up. |
