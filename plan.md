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
|[`Hero`](sim/Components/UnitTypes.cs)|[`Turret`](sim/Components/UnitTypes.cs) |[`Minion`](sim/Components/UnitTypes.cs)|[`Crystal`](sim/Components/UnitTypes.cs) | Shop |
|-|-|-|-|-|



## Network Ids

### [`KlothoComponent`](vendor/Klotho/com.xpturn.klotho/Runtime/ECS/Core/IComponent.cs)

Uses [`KlothoComponentAttribute`](vendor/Klotho/com.xpturn.klotho/Runtime/ECS/Attributes/KlothoComponentAttribute.cs) for network IDs.

Component ids live in code, not here: [`ComponentIds`](sim/Components/ComponentIds.cs) is the single
allocation ledger, kept in numeric order with the next free id at the bottom. Components reference it
by name (`[KlothoComponent(ComponentIds.Hero)]`), so this doc no longer duplicates the numbers.

Components are grouped by domain under [`sim/Components/`](sim/Components):

| File | Components |
| ---- | ---------- |
| [`Identity.cs`](sim/Components/Identity.cs) | `Player`, `Unit`, `Team`, `Faction`, `Controllable` |
| [`UnitTypes.cs`](sim/Components/UnitTypes.cs) | `Hero`, `Minion`, `Turret`, `Crystal`, `SpawnPoint` |
| [`Combat.cs`](sim/Components/Combat.cs) | `Health`, `Combat`, `AttackTargetUnitId`, `PendingRespawn` |
| [`Movement.cs`](sim/Components/Movement.cs) | `UnitMoveTarget`, `MinionSettleTracker` |
| [`Economy.cs`](sim/Components/Economy.cs) | `Inventory`, `Pickup`, `Oasis`, `OasisEjectPending`, `OasisResourceLanding` |
| [`Match.cs`](sim/Components/Match.cs) | `PlayerFaction`, `Stats` |
| [`Singletons.cs`](sim/Components/Singletons.cs) | `UnitIdCounter`, `PickupIdCounter`, `MatchSetupState` |

### [`KlothoSerializable`](vendor/Klotho/com.xpturn.klotho/Runtime/Serialization/Attributes/KlothoSerializableAttribute.cs)

| Commands | |
| -- | ---- |
| 100 | [`MoveCommand`](sim/Commands/MoveCommand.cs) |
| 103 | [`AttackCommand`](sim/Commands/AttackCommand.cs) |
| 104 | [`SelectFactionCommand`](sim/Commands/SelectFactionCommand.cs) |
| 105 | [`ModifyStatCommand`](sim/Commands/ModifyStatCommand.cs) |
| 106 | [`PurchaseItemCommand`](sim/Commands/PurchaseItemCommand.cs) |
| **Events** | |
| 101 | [`GameOverEvent`](sim/Events/GameOverEvent.cs) |
| 102 | [`UnitDiedEvent`](sim/Events/UnitDiedEvent.cs) |
| 104 | [`PlayerDiedEvent`](sim/Events/PlayerDiedEvent.cs) |
| 105 | [`PlayerRespawnedEvent`](sim/Events/PlayerRespawnedEvent.cs) |
| 106 | [`CrystalDestroyedEvent`](sim/Events/CrystalDestroyedEvent.cs) |
| 107 | [`TurretDestroyedEvent`](sim/Events/TurretDestroyedEvent.cs) |

### [`KlothoDataAsset`](vendor/Klotho/com.xpturn.klotho/Runtime/ECS/DataAsset/IDataAsset.cs)

Uses [`KlothoDataAssetAttribute`](vendor/Klotho/com.xpturn.klotho/Runtime/ECS/DataAsset/KlothoDataAssetAttribute.cs) for network IDs.

| 100           | 101         | 102         | 103           | 104         | 105         | 106           | 107            | 108         |
| ------------- | ----------- | ----------- | ------------- | ----------- | ----------- | ------------- | -------------- | ----------- |
| [`PlayerStats`](sim/Assets/PlayerStatsAsset.cs) | [`WaveRules`](sim/Assets/WaveRulesAsset.cs) | [`MapLayout`](sim/Assets/MapLayoutAsset.cs) | [`MinionStats`](sim/Assets/MinionStatsAsset.cs) | [`Faction`](sim/Assets/FactionAsset.cs) | [`ShopItem`](sim/Assets/ShopItemAsset.cs) | [`TurretStats`](sim/Assets/TurretStatsAsset.cs) | [`CrystalStats`](sim/Assets/CrystalStatsAsset.cs) | [`ShopRules`](sim/Assets/ShopRulesAsset.cs) |

`Faction` is a multi-instance catalog asset (type id 104): one instance per faction, keyed by its own `AssetId` in the 200 range (`Get<FactionAsset>(factionId)`). See `client/Sim/Data/Assets.json`.

`ShopItem` is likewise a multi-instance catalog asset (type id 105): one instance per purchasable item, keyed by its own `AssetId` in the 300 range (`Get<ShopItemAsset>(itemId)`). Sim owns Cost/AttackBonus; the client [`ShopItemCatalog`](client/Scripts/View/ShopItemCatalog.cs) maps those AssetIds to portraits/names.

`ShopRules` is the singleton gate for shop interaction: its `InteractRange` is read by [`CommandSystem`](sim/Systems/CommandSystem.cs) as the authoritative purchase check and by the client [`ActionBarController`](client/Scripts/UI/ActionBarController.cs) as the UI proximity hint, so the two can never disagree.

### Klotho Internal

| ID  | Name                | Notes                                                          |
| --- | ------------------- | -------------------------------------------------------------- |
| 11  | [`NavAgentComponent`](vendor/Klotho/com.xpturn.klotho/Runtime/Deterministic/Navigation/NavAgentComponent.cs) | Packaged Klotho nav component; no conflict with project range. |

Next free project IDs: [`KlothoComponent`](vendor/Klotho/com.xpturn.klotho/Runtime/ECS/Core/IComponent.cs) 117, [`KlothoSerializable`](vendor/Klotho/com.xpturn.klotho/Runtime/Serialization/Attributes/KlothoSerializableAttribute.cs) command 107, [`KlothoSerializable`](vendor/Klotho/com.xpturn.klotho/Runtime/Serialization/Attributes/KlothoSerializableAttribute.cs) event 108, [`KlothoDataAsset`](vendor/Klotho/com.xpturn.klotho/Runtime/ECS/DataAsset/IDataAsset.cs) type id 106 (faction instance AssetIds use the 200 range, shop item AssetIds the 300 range).

## Systems

| System | Notes |
| ------ | ----- |
| **Commands** | |
| [`CommandSystem`](sim/Systems/CommandSystem.cs) | Validates+applies commands (incl. [`SelectFactionCommand`](sim/Commands/SelectFactionCommand.cs) → [`PlayerFaction`](sim/Components/Match.cs)) |
| **Spawning** | |
| [`HeroSpawnSystem`](sim/Systems/HeroSpawnSystem.cs) | Spawns each player's hero once their faction pick lands (or after a grace window w/ default). Heroes carry [`Faction`](sim/Components/Identity.cs) at spawn so the view resolves the faction scene |
| [`WaveSpawnSystem`](sim/Systems/WaveSpawnSystem.cs) | Spawns minion waves from [`SpawnPoint`](sim/Components/UnitTypes.cs) markers |
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
| 1 | Flow fields for group pathing. A* for heroes
| 2 | ORCA for local avoidance O(n)|
| 4 | Subgrid used for local avoidance |
| 5 | Hierarchical decomposition | Sector/portal graph for macro routing avoids computing detailed paths across the whole map |

## Todo

| Status | Work |
| ------ | ------------------------------------------------------------------------------------------------------------- |
| ⬜ | Add event-driven audio reactions from synced events, not gameplay authority. |
| ⬜ | Add model/team tinting for structure views. |
| ⬜ | Force propagation (group arrival) — "transitive bumping": first unit reaching destination broadcasts completion, adjacent units recognize arrival and cascade outward. Prevents pile-ups at destinations. |
| ⬜ | Branchless code — `math.select` instead of `if/else` for flow field generation (reported 10x speedup in RTS literature). |
| ⬜ | Multi-threaded A* — leverage 6-8 cores for parallel path queries. |
