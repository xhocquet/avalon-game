# Avalon

- [`sim/`](sim/) - deterministic sim used by both client and server
- [`client/Sim/Data/`](client/Sim/Data/) - shared data including JSON and map data
   - Godot IO (`res://`) requires data to live in the client directory
- [`client/`](client/) - Godot project, main clients
- [`server/`](server/) - C# server. Fixed tick simulation receiving commands and sending out events.
- [`vendor/Klotho/`](vendor/Klotho/) - Networking library, forked

## Simulation Config

Authoritative multiplayer config lives in [`server/simulationconfig.json`](server/simulationconfig.json). Avalon is tuned as a Footmen Frenzy-style server-driven RTS/arena strategy game: multiple players, many controllable units, fairness-first deterministic outcomes, and shallow rollback to avoid expensive many-entity resimulation.

| Setting | Value | Reason |
| ------- | ----- | ------ |
| `Mode` | `ServerDriven` | Server authority for validation, fairness, and room management. |
| `TickIntervalMs` | `66` | ~15 Hz sim cadence; more scalable than action-style 60 Hz while staying more responsive than a full 100 ms RTS tick. |
| `InputDelayTicks` | `4` | Adds jitter slack without making command feel as heavy as the RTS guide's 6 tick baseline. |
| `SDInputLeadTicks` | `4` | Gives client inputs enough lead time to reach the authoritative server. |
| `MaxRollbackTicks` | `8` | Shallow versus the old 50-tick action profile, but high enough for Klotho's server-driven input lead and sync-check invariants. |
| `SyncCheckInterval` | `4` | Fast desync detection for fairness; must stay within Klotho's effective `MaxRollbackTicks / 2` sync window. |
| `UsePrediction` | `false` | Avoids speculative many-unit correction churn. |
| `EnableErrorCorrection` | `false` | Prefer visible authoritative state over smoothing that can hide deterministic issues. |
| `MaxEntities` | `1024` | Current baseline for hundreds of units plus structures, players, and transient sim entities. |

Revisit these values after crowded-wave profiling. If input feels too heavy, try `TickIntervalMs = 50` before increasing rollback depth. If server arrival jitter causes missed inputs, raise `SDInputLeadTicks` before lowering fairness checks.

## Network Ids

### [`KlothoComponent`](vendor/Klotho/com.xpturn.klotho/Runtime/ECS/Core/IComponent.cs)

Uses [`KlothoComponentAttribute`](vendor/Klotho/com.xpturn.klotho/Runtime/ECS/Attributes/KlothoComponentAttribute.cs) for network IDs.

| Core | |
| -- | --------- |
| 100 | [`Player`](sim/Models/Player.cs) |
| 102 | [`Team`](sim/Models/Team.cs) |
| 103 | [`Health`](sim/Models/Health.cs) |
| 107 | [`SpawnPoint`](sim/Models/SpawnPoint.cs) |
| 108 | [`Combat`](sim/Models/Combat.cs) |
| 110 | [`UnitMoveTarget`](sim/Models/UnitMoveTarget.cs) |
| 111 | [`AttackTargetUnitId`](sim/Models/AttackTargetUnitId.cs) |
| 112 | [`PendingRespawn`](sim/Models/PendingRespawn.cs) |
| **Units** | |
| 101 | [`Unit`](sim/Models/Unit.cs) |
| 104 | [`Hero`](sim/Models/Hero.cs) |
| 105 | [`Minion`](sim/Models/Minion.cs) |
| 106 | [`Crystal`](sim/Models/Crystal.cs) |
| 113 | [`Turret`](sim/Models/Turret.cs) |
| **Singletons** | |
| 109 | [`UnitIdCounter`](sim/Models/UnitIdCounter.cs) |

### [`KlothoSerializable`](vendor/Klotho/com.xpturn.klotho/Runtime/Serialization/Attributes/KlothoSerializableAttribute.cs)

| Commands | |
| -- | ---- |
| 100 | [`MoveCommand`](sim/Commands/MoveCommand.cs) |
| 103 | [`AttackCommand`](sim/Commands/AttackCommand.cs) |
| **Events** | |
| 101 | [`GameOverEvent`](sim/Events/GameOverEvent.cs) |
| 102 | [`UnitDiedEvent`](sim/Events/UnitDiedEvent.cs) |
| 104 | [`PlayerDiedEvent`](sim/Events/PlayerDiedEvent.cs) |
| 105 | [`PlayerRespawnedEvent`](sim/Events/PlayerRespawnedEvent.cs) |
| 106 | [`CrystalDestroyedEvent`](sim/Events/CrystalDestroyedEvent.cs) |
| 107 | [`TurretDestroyedEvent`](sim/Events/TurretDestroyedEvent.cs) |

### [`KlothoDataAsset`](vendor/Klotho/com.xpturn.klotho/Runtime/ECS/DataAsset/IDataAsset.cs)

Uses [`KlothoDataAssetAttribute`](vendor/Klotho/com.xpturn.klotho/Runtime/ECS/DataAsset/KlothoDataAssetAttribute.cs) for network IDs.

| 100           | 101         | 102         | 103           |
| ------------- | ----------- | ----------- | ------------- |
| [`PlayerStats`](sim/Assets/PlayerStatsAsset.cs) | [`WaveRules`](sim/Assets/WaveRulesAsset.cs) | [`MapLayout`](sim/Assets/MapLayoutAsset.cs) | [`MinionStats`](sim/Assets/MinionStatsAsset.cs) |

### Klotho Internal

| ID  | Name                | Notes                                                          |
| --- | ------------------- | -------------------------------------------------------------- |
| 11  | [`NavAgentComponent`](vendor/Klotho/com.xpturn.klotho/Runtime/Deterministic/Navigation/NavAgentComponent.cs) | Packaged Klotho nav component; no conflict with project range. |

Next free project IDs: [`KlothoComponent`](vendor/Klotho/com.xpturn.klotho/Runtime/ECS/Core/IComponent.cs) 114, [`KlothoSerializable`](vendor/Klotho/com.xpturn.klotho/Runtime/Serialization/Attributes/KlothoSerializableAttribute.cs) command 104, [`KlothoSerializable`](vendor/Klotho/com.xpturn.klotho/Runtime/Serialization/Attributes/KlothoSerializableAttribute.cs) event 108, [`KlothoDataAsset`](vendor/Klotho/com.xpturn.klotho/Runtime/ECS/DataAsset/IDataAsset.cs) 104.

## Systems

| System | Notes |
| ------ | ----- |
| **Commands** | |
| [`CommandSystem`](sim/Systems/CommandSystem.cs) | Validates and applies [`MoveCommand`](sim/Commands/MoveCommand.cs) and [`AttackCommand`](sim/Commands/AttackCommand.cs); writes [`UnitMoveTarget`](sim/Models/UnitMoveTarget.cs) and [`AttackTargetUnitId`](sim/Models/AttackTargetUnitId.cs). |
| **Spawning** | |
| [`WaveSpawnSystem`](sim/Systems/WaveSpawnSystem.cs) | Spawns deterministic team minion waves from [`SpawnPoint`](sim/Models/SpawnPoint.cs) markers using wave/minion data assets. |
| **Combat** | |
| [`TargetAcquisitionSystem`](sim/Systems/TargetAcquisitionSystem.cs) | Gives eligible combat units autonomous enemy acquisition when they have no attack target or move target. |
| [`AttackIntentSystem`](sim/Systems/AttackIntentSystem.cs) | Resolves attack targets each tick, chases moving targets, and clears invalid intent when targets die or become invalid. |
| [`AttackCooldownSystem`](sim/Systems/AttackCooldownSystem.cs) | Decrements attack cooldowns. |
| [`DamageSystem`](sim/Systems/DamageSystem.cs) | Applies deterministic cooldown-gated damage for in-range attack targets. |
| **Lifecycle** | |
| [`DeathSystem`](sim/Systems/DeathSystem.cs) | Removes dead non-player units and raises unit, crystal, or turret death/destruction events. |
| [`RespawnSystem`](sim/Systems/RespawnSystem.cs) | Owns player death, scrubs active state during [`PendingRespawn`](sim/Models/PendingRespawn.cs), respawns after 5 seconds, and resets nav agents. |
| [`EventSystem`](vendor/Klotho/com.xpturn.klotho/Runtime/ECS/Systems/EventSystem.cs) | Klotho runtime system that dispatches raised simulation events. |
| **Navigation** | |
| [`NavigationAgentSystem`](sim/Systems/NavigationAgentSystem.cs) | Consumes [`UnitMoveTarget`](sim/Models/UnitMoveTarget.cs), runs [`NavigationRuntime.AgentSystem`](sim/NavigationRuntime.cs), and syncs [`NavAgentComponent`](vendor/Klotho/com.xpturn.klotho/Runtime/Deterministic/Navigation/NavAgentComponent.cs) back to transforms. |
| [`FPNavAgentSystem`](vendor/Klotho/com.xpturn.klotho/Runtime/Deterministic/Navigation/FPNavAgentSystem.cs) | Runtime helper owned by [`NavigationRuntime`](sim/NavigationRuntime.cs); [`NavigationAgentSystem`](sim/Systems/NavigationAgentSystem.cs) calls it when nav bytes are loaded. |
| [`FPNavAvoidance`](vendor/Klotho/com.xpturn.klotho/Runtime/Deterministic/Navigation/FPNavAvoidance.cs) | ORCA avoidance helper instantiated by [`NavigationRuntime`](sim/NavigationRuntime.cs) and assigned to [`FPNavAgentSystem`](vendor/Klotho/com.xpturn.klotho/Runtime/Deterministic/Navigation/FPNavAgentSystem.cs). |
| **Match** | |
| [`ScoreSystem`](sim/Systems/ScoreSystem.cs) | Evaluates timeout and crystal win conditions, writes Klotho match-end state, and raises one-shot payload-free [`GameOverEvent`](sim/Events/GameOverEvent.cs). |
| **Server** | |
| [`MatchResultSaveSystem`](server/MatchResultSaveSystem.cs) | Server-only post-update system that saves the shared [`MatchResultReader`](sim/MatchResult.cs) output once per ended match. |

## Shared Client Server

- [`SimMarkerNode`](client/Scripts/SimMarkerNode.cs) ([Tool][GlobalClass] Node3D) places sim layout markers in the editor.
- [`MapLayoutAsset`](sim/Assets/MapLayoutAsset.cs) ([`KlothoDataAsset`](vendor/Klotho/com.xpturn.klotho/Runtime/ECS/DataAsset/IDataAsset.cs) 102) stores marker positions; [`GodotFPMapLayoutExporter`](client/Scripts/Editor/GodotFPMapLayoutExporter.cs) bakes them to [`Sim/Data/MapLayout.bytes`](client/Sim/Data/MapLayout.bytes).
- [`SimulationSetup`](sim/SimulationSetup.cs) requires [`MapLayoutAsset`](sim/Assets/MapLayoutAsset.cs) for structure/spawn positions and fails loudly when markers are missing.
- Map layout foundation is complete: editor-authored markers can drive baked layout data for sim spawn/structure placement.
- Client and server load [`MapLayout.bytes`](client/Sim/Data/MapLayout.bytes) as runtime data.
- Navigation runtime exists: [`FPNavMesh`](vendor/Klotho/com.xpturn.klotho/Runtime/Deterministic/Navigation/FPNavMesh.cs) loading, query/pathfinder/funnel, and sim integration.
- [`NavigationRegion3D.NavMeshData.bytes`](client/Sim/Data/NavigationRegion3D.NavMeshData.bytes) lives beside [`MapLayout.bytes`](client/Sim/Data/MapLayout.bytes) under [`client/Sim/Data/`](client/Sim/Data/); client and server load it from that shared data path.
- Client and server register [`NavigationRuntime.FromBytes(...)`](sim/NavigationRuntime.cs) before world initialization.

## Determinism

- [`UnitIdGenerator`](sim/UnitIdGenerator.cs) provides stable sim-level unit identity.

## Node Types

| Status | Node Type    | Sim State                                                                  | View / Layout                                                         | Notes                                                                                                           |
| ------ | ------------ | -------------------------------------------------------------------------- | --------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------- |
| ✅      | Hero / Champ | [`Hero`](sim/Models/Hero.cs), [`Player`](sim/Models/Player.cs), [`Health`](sim/Models/Health.cs), [`Combat`](sim/Models/Combat.cs), [`NavAgentComponent`](vendor/Klotho/com.xpturn.klotho/Runtime/Deterministic/Navigation/NavAgentComponent.cs), stable [`UnitId`](sim/Models/Unit.cs) | Hero/champ view scenes via [`UnitViewFactory`](client/Scripts/View/UnitViewFactory.cs)                          | Main controllable character; currently carries [`Player`](sim/Models/Player.cs) participant state and respawns through [`RespawnSystem`](sim/Systems/RespawnSystem.cs). |
| ✅      | Minion       | [`Minion`](sim/Models/Minion.cs), [`Health`](sim/Models/Health.cs), [`Combat`](sim/Models/Combat.cs), [`NavAgentComponent`](vendor/Klotho/com.xpturn.klotho/Runtime/Deterministic/Navigation/NavAgentComponent.cs), stable [`UnitId`](sim/Models/Unit.cs)         | Minion waves via [`WaveSpawnSystem`](sim/Systems/WaveSpawnSystem.cs); minion view via [`UnitViewFactory`](client/Scripts/View/UnitViewFactory.cs) | Wave-spawned controllable unit.                                                                                 |
| ✅      | Turret       | [`Turret`](sim/Models/Turret.cs), [`Health`](sim/Models/Health.cs), [`Combat`](sim/Models/Combat.cs), team ownership, stable [`UnitId`](sim/Models/Unit.cs)              | [`MapMarkerType.Turret`](sim/MapMarkerType.cs); turret view via [`UnitViewFactory`](client/Scripts/View/UnitViewFactory.cs)             | Stationary combat structure; acquires targets but does not chase.                                               |
| ✅      | Crystal      | [`Crystal`](sim/Models/Crystal.cs), [`Health`](sim/Models/Health.cs), team ownership, stable [`UnitId`](sim/Models/Unit.cs)                       | [`MapMarkerType.Crystal`](sim/MapMarkerType.cs); crystal view via [`UnitViewFactory`](client/Scripts/View/UnitViewFactory.cs)           | Team core structure; destruction emits [`CrystalDestroyedEvent`](sim/Events/CrystalDestroyedEvent.cs).                                                 |
| 🟡      | Shop         | [`MapMarkerType.Shop`](sim/MapMarkerType.cs) in baked [`MapLayoutAsset`](sim/Assets/MapLayoutAsset.cs); no gameplay component yet  | Existing world-scene shop marker/view                                 | Marker is exported for future game logic.                                                                       |

## UI

| Status | UI           | Source State | Notes                                           |
| ------ | ------------ | ------------ | ----------------------------------------------- |
| ✅      | [`HealthBars`](client/Scripts/UI/HealthBars.cs) | [`Health`](sim/Models/Health.cs)     | Renders for all live view entities with health. |
| ⬜      | Minimap      | TBD          | Match/map awareness UI remains.                 |


## Click Orders

| Input                  | Command / State                | Payload                                                  | Sim Handling                                                                                                     |
| ---------------------- | ------------------------------ | -------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------- |
| Local click selection  | Client-only selection state    | Selected view/unit ids are not recorded as sim state.    | No sim mutation.                                                                                                 |
| Right-click ground     | [`MoveCommand`](sim/Commands/MoveCommand.cs)                  | Target X/Z plus explicit selected [`UnitIds`](sim/Models/Unit.cs).             | [`CommandSystem`](sim/Systems/CommandSystem.cs) validates ownership and writes [`UnitMoveTarget`](sim/Models/UnitMoveTarget.cs).                                                 |
| Right-click enemy      | [`AttackCommand`](sim/Commands/AttackCommand.cs)                | Target [`UnitId`](sim/Models/Unit.cs) plus explicit selected source [`UnitIds`](sim/Models/Unit.cs). | [`CommandSystem`](sim/Systems/CommandSystem.cs) validates ownership/live enemy target and writes [`AttackTargetUnitId`](sim/Models/AttackTargetUnitId.cs) plus initial chase target. |
| WASD / camera controls | Client-only camera/debug input | No gameplay command payload.                             | No sim mutation.                                                                                                 |

## Milestone E: Avoidance And Scale

Goal: scale toward hundreds or thousands of units without physics. Nav paths are handled by [`FPNavAgentSystem`](vendor/Klotho/com.xpturn.klotho/Runtime/Deterministic/Navigation/FPNavAgentSystem.cs); this milestone is about agent separation and iteration cost.

| Status | Work                                                                                                 |
| ------ | ---------------------------------------------------------------------------------------------------- |
| ⬜      | Record a crowded-wave benchmark with [`FPNavAvoidance`](vendor/Klotho/com.xpturn.klotho/Runtime/Deterministic/Navigation/FPNavAvoidance.cs) enabled.                                       |
| ⬜      | If avoidance dominates frame time, add a config gate or cheaper settings and document the threshold. |
| ⬜      | Add a spatial grid for combat proximity scans only if profiling shows a real hotspot.                |
| ✅      | Keep all iteration order stable.                                                                     |

| Status | Acceptance                                                       |
| ------ | ---------------------------------------------------------------- |
| ⬜      | Large minion counts remain stable and cheap.                     |
| ✅      | No dynamic physics bodies are required for normal unit movement. |

## Todo, No Particular Order

| Status | Work                                                                                                          |
| ------ | ------------------------------------------------------------------------------------------------------------- |
| ⬜      | Add event-driven VFX reactions from synced events, not gameplay authority.                                    |
| ⬜      | Add event-driven audio reactions from synced events, not gameplay authority.                                  |
| ⬜      | Add model/team tinting for structure views.                                                                  |
| ⬜      | Add minimap UI.                                                            |
| ⬜      | Add a bounded async logging sink for server diagnostics.                                                      |
| ⬜      | Add a dynamic view/object pool for minions; a fixed 64-object pool is probably too small once waves stack up. |
