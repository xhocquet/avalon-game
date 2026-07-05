# Avalon
- [`sim/`](sim/) - deterministic sim used by both client and server
- [`client/Sim/Data/`](client/Sim/Data/) - shared data including JSON and map data
   - Godot IO (`res://`) requires data to live in the client directory
- [`client/`](client/) - Godot project, main clients
- [`server/`](server/) - C# server. Fixed tick simulation receiving commands and sending out events.
- [`vendor/Klotho/`](vendor/Klotho/) - Networking library, forked

## Tools
- [`SimMarkerNode`](client/Scripts/SimMarkerNode.cs) - Markers can be placed in Godot and used in sim code (spawns, shops)
  - These are baked with Godot tool [`GodotFPMapLayoutExporter`](client/Scripts/Editor/GodotFPMapLayoutExporter.cs) and saved to [`Sim/Data/MapLayout.bytes`](client/Sim/Data/MapLayout.bytes)
- In the same way, we generate a deterministic navmesh to [`NavigationRegion3D.NavMeshData.bytes`](client/Sim/Data/NavigationRegion3D.NavMeshData.bytes)
- [`UnitIdGenerator`](sim/UnitIdGenerator.cs) provides stable identifiers for all units

## Testing
- `just smoke` — boots server + 2 headless Godot clients, asserts [`=== CLIENT OK ===`](scripts/smoke.ps1) at tick 120 with no sim exceptions
- `just test` — xunit suite ([`SimInvariantTests`](tests/Avalon.Sim.Tests/SimInvariantTests.cs), [`DeterminismBaselineTests`](tests/Avalon.Sim.Tests/DeterminismBaselineTests.cs), combat, death, nav, scoring, spatial grid)
  - Uses [`SimHarness`](tests/Avalon.Sim.Tests/SimHarness.cs) to bootstrap a full sim with no Godot dependency
- `just loadtest` (default 1K ticks) or `just loadtest 10000` — [`LoadTestHarness`](tests/Avalon.Sim.Tests/LoadTestHarness.cs) runs headless sim, reports per-system timings from Klotho's [`ConsumeUpdateTimings`](vendor/Klotho/com.xpturn.klotho/Runtime/ECS/System/SystemRunner.cs) every 500 ticks
- `just loadtest-profile` (default 10K ticks) or `just loadtest-profile 5000` — wraps `dotnet-trace` around the load test, converts to Speedscope flame graph at `TestResults/loadtest/loadtest_profile.speedscope.json`

## Simulation Config

[`server/simulationconfig.json`](server/simulationconfig.json)

| `TickIntervalMs` | `66` | ~15 Hz (action 60 Hz, RTS 100 ms) |
| ------- | ----- | ------ |
| `InputDelayTicks` | `4` | Adds jitter slack without making command feel as heavy as the RTS guide's 6 tick baseline |
| `SDInputLeadTicks` | `4` | Gives clients enough lead time to reach the authoritative server. |
| `MaxRollbackTicks` | `8` | Just enough to smooth inputs |
| `SyncCheckInterval` | `4` | Fast desync detection |
| `UsePrediction` | `false` | Too expensive for many units |
| `EnableErrorCorrection` | `false` | Correct visible state > smoothing |
| `MaxEntities` | `1024+` | Gives us 4 players X 256 units. Room to grow |



## Node Types
|[`Hero`](sim/Models/Hero.cs)|[`Turret`](sim/Models/Turret.cs) |[`Minion`](sim/Models/Minion.cs)|[`Crystal`](sim/Models/Crystal.cs) | Shop |
|-|-|-|-|-|



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
| 114 | [`Controllable`](sim/Models/Controllable.cs) |
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

Next free project IDs: [`KlothoComponent`](vendor/Klotho/com.xpturn.klotho/Runtime/ECS/Core/IComponent.cs) 115, [`KlothoSerializable`](vendor/Klotho/com.xpturn.klotho/Runtime/Serialization/Attributes/KlothoSerializableAttribute.cs) command 104, [`KlothoSerializable`](vendor/Klotho/com.xpturn.klotho/Runtime/Serialization/Attributes/KlothoSerializableAttribute.cs) event 108, [`KlothoDataAsset`](vendor/Klotho/com.xpturn.klotho/Runtime/ECS/DataAsset/IDataAsset.cs) 104.

## Systems

| System | Notes |
| ------ | ----- |
| **Commands** | |
| [`CommandSystem`](sim/Systems/CommandSystem.cs) | Validates+applies commands |
| **Spawning** | |
| [`WaveSpawnSystem`](sim/Systems/WaveSpawnSystem.cs) | Spawns minion waves from [`SpawnPoint`](sim/Models/SpawnPoint.cs) markers |
| **Combat** | |
| [`TargetAcquisitionSystem`](sim/Systems/TargetAcquisitionSystem.cs) | Gives units auto enemy focus when they have no active target |
| [`AttackIntentSystem`](sim/Systems/AttackIntentSystem.cs) | Resolves attack targets, chases targets, and clears targets |
| [`AttackCooldownSystem`](sim/Systems/AttackCooldownSystem.cs) | Decrements attack cooldowns |
| [`DamageSystem`](sim/Systems/DamageSystem.cs) | Applies damage when a valid target is in range |
| **Lifecycle** | |
| [`DeathSystem`](sim/Systems/DeathSystem.cs) | Handles all non-hero deaths and death events |
| [`RespawnSystem`](sim/Systems/RespawnSystem.cs) | Handles hero death, respawn, and data cleanup |
| [`EventSystem`](vendor/Klotho/com.xpturn.klotho/Runtime/ECS/Systems/EventSystem.cs) | Klotho runtime system that dispatches raised simulation events. |
| **Navigation** | |
| [`NavigationAgentSystem`](sim/Systems/NavigationAgentSystem.cs) | Runs [`NavigationRuntime.AgentSystem`](sim/NavigationRuntime.cs), and syncs [`NavAgentComponent`](vendor/Klotho/com.xpturn.klotho/Runtime/Deterministic/Navigation/NavAgentComponent.cs) back to transforms. |
| [`FPNavAgentSystem`](vendor/Klotho/com.xpturn.klotho/Runtime/Deterministic/Navigation/FPNavAgentSystem.cs) | Runtime helper owned by [`NavigationRuntime`](sim/NavigationRuntime.cs) |
| [`FPNavAvoidance`](vendor/Klotho/com.xpturn.klotho/Runtime/Deterministic/Navigation/FPNavAvoidance.cs) | ORCA avoidance helper |
| **Match** | |
| [`ScoreSystem`](sim/Systems/ScoreSystem.cs) | Tracks score, win conditions,  and raises [`GameOverEvent`](sim/Events/GameOverEvent.cs) |
| [`MatchResultSaveSystem`](server/MatchResultSaveSystem.cs) | Server-only. Saves [`MatchResultReader`](sim/MatchResult.cs) output on match end |



## Navigation Research Takeaways

| # | Insight | Detail |
|---|---------|--------|
| 1 | Flow fields for group pathing | One computation serves all units heading to same destination — amortizes cost across N units instead of N×A* |
| 2 | ORCA for local avoidance | Industry standard (SC2 uses it); handles thousands of agents in ms via 2D linear programming in velocity space |
| 3 | Temporal spreading is critical | Never pathfind all units in the same frame; distribute requests across ticks |
| 4 | Spatial hashing is the right foundation | Already have spatial grid — use it for neighbor queries in avoidance |
| 5 | Hierarchical decomposition | Sector/portal graph for macro routing avoids computing detailed paths across the whole map |

## Todo

| Status | Work |
| ------ | ------------------------------------------------------------------------------------------------------------- |
| ⬜      | Add event-driven VFX reactions from synced events, not gameplay authority.|
| ⬜      | Add event-driven audio reactions from synced events, not gameplay authority. |
| ⬜      | Add model/team tinting for structure views. |
| ⬜      | Add minimap UI.|
| ⬜      | Redo camera|
| ⬜      | Add a bounded async logging sink for server diagnostics.|
| ⬜      | Add a dynamic view/object pool for minions; a fixed 64-object pool is probably too small once waves stack up. |
| ⬜      | Check client-s ide frame cost with Godot's built-in Profiler/Monitors if server-side tick timing doesn't fully explain the choppiness. |
| ⬜      | Investigate [`TargetAcquisitionSystem`](sim/Systems/TargetAcquisitionSystem.cs) cost spikes (2-4ms at 200+ entities) despite grid — likely high local density when units cluster in lane fights; consider a neighbor cap or larger cell size. |
