# Shared Simulation Scope

- `sim/` is authoritative deterministic gameplay compiled by both `client/` and `server/`. Keep client/server behavior aligned when editing here.
- Data assets live at `client/Sim/Data/` because Godot `res://` requires them inside the Godot project. The server copies those `.bytes` files at build/runtime.
- Both sides call `SimulationSetup.RegisterSystems(...)` and `SimulationSetup.InitializeWorld(...)` through their `ISimulationCallbacks` implementations.
- Godot client callbacks poll local input and send commands; server callbacks do not poll local input because Klotho injects client commands into the authoritative server simulation.

# Heroes

- A hero's numbers come from [`HeroAsset`](Assets/HeroAsset.cs) (rows in the `AssetIds.Hero*` block), reached via `FactionAsset.HeroAssetId`. `PlayerStatsAsset` is gone: its hero stats moved here and its gold fields to `MatchRulesAsset`.
- An asset row is only the **spawn seed**. Live values that skills and items change belong on a component, and changes route through `Stats.Add(StatType, delta)` rather than writing fields. Never read a live value back off the asset.
  - `MoveSpeed` → `Stats.MoveSpeed`; `NavigationAgentSystem` pushes it onto the nav agent every tick, `CommandSystem`'s direct-move path reads it.
  - `AttackDamage` → `Stats.Strength`; `AttackRange` → `Combat.AttackRange`; `Health` → `Stats.MaxHealth` (`Health` holds only the current HP, which is transient state rather than a buffable stat).
  - `AttackCooldownTicks` is the **base period** and stays put in `Combat`; `Stats.AttackSpeed` is the multiplier, and `DamageSystem` divides at the moment of the hit so bonuses stay additive on the rate and rounding never compounds.
- The spawned entity stores `Hero.HeroAssetId`, so any system can get back to the row without going through the faction.
- Hero-specific *code* lives in an [`IHeroBehavior`](Heroes/IHeroBehavior.cs) selected by `HeroAsset.BehaviorId` through [`HeroBehaviors.Get`](Heroes/HeroBehaviors.cs). `HeroFactory` calls `OnSpawn`; [`HeroBehaviorSystem`](Systems/HeroBehaviorSystem.cs) calls `OnTick` for every hero each tick.
- Behaviors are **stateless singletons**. Components are the only rollback-safe storage — a field on a behavior survives a rollback and desyncs the client. Add a component in `OnSpawn` and mutate it in `OnTick`.
- Components can't be subclassed (they are `[StructLayout(Sequential)]` structs snapshot by value), and adding a hero must never change the component layout heroes share.
- Adding a hero: allocate an id in `AssetIds`, add the row to `Assets.json`, regenerate `Assets.bytes`, point a `FactionAsset` at it. Code is only needed when it wants behavior no existing `BehaviorId` covers.

# Navigation & Temporal Spreading

- `NavigationAgentSystem` handles all unit movement: hero A* pathfinding, minion flow-field steering, ORCA avoidance, and movement integration.
- **Temporal spreading** distributes expensive phases across frames via `NavigationTuningAsset` (row `AssetId: 112` in `client/Sim/Data/Assets.json`):
  - `HeroSteeringSpread` — A* steering update interval (default 1 = every tick)
  - `MinionSteeringSpread` — flow field steering interval (default 1)
  - `AvoidanceSpread` — ORCA collision avoidance interval (default 1)
  - Set to N to update 1/N of agents per tick. Phases are offset (0, 1, 2) so they don't spike the same frame.
- The same asset holds the steering/settle tuning (arrival radii, brake distance, blocked/stuck settle thresholds, ORCA neighbour radius and time horizon, nav-agent acceleration factor). Distances are authored linearly; the system squares them per tick.
- `NavigationRuntime` is built before any frame exists, so it leaves ORCA at Klotho defaults; `NavigationAgentSystem` pushes `AvoidanceTimeHorizon` onto it each tick.
- Movement integration and transform sync run every tick regardless of spread, keeping positions smooth.
- spread=N multiplies each agent's steering refresh interval by N ticks (see `server/simulationconfig.json` for the tick rate). Small values stay within the genre's acceptable latency for AI-controlled units.

# Working Rules

- Prefer compact intent commands with stable `Unit.UnitId` references. Do not put transient ECS entity ids in command payloads.
- Movement is planar: `TransformComponent.Position.x/z`.
- NO dynamic physics. Use deterministic transform integration, radii, proximity queries, grids, and stable iteration order.
- When changing gameplay rules, inspect `sim/` first instead of duplicating logic in `client/` or `server/`.
- Klotho asset id ranges (both the AssetId and wire TypeId planes) are tracked in `sim/Assets/AssetIds.cs`; allocate from the "next free" markers there.
- Systems hold no tuning constants. Gameplay numbers live in `client/Sim/Data/Assets.json` and are read through `frame.AssetRegistry.Get<T>()`; after editing the JSON run `just` asset generation (`dotnet run --project tools/AssetGen`) to rebuild `Assets.bytes`.

# Command Validation

Command payloads come off the wire from untrusted peers, and nothing between the socket and the simulation catches an exception — LiteNetLib's `ProcessEvent`, `ServerNetworkService.HandleClientInputMessage`, and `ServerLoop.ExecuteCycle` all let one propagate, and `ServerLoop.Run` has no `catch`. A command that throws or corrupts state while deserializing takes the server process down with every room on it, so validation is not optional for a new command type.

Every command passes through two layers before a handler runs:

1. **Structural**, inside the command's own `DeserializeData`. A variable-length field must never size a buffer or advance the reader from an unchecked wire count. See [`UnitIdList.Deserialize`](Commands/UnitIdList.cs) for the pattern: reject a negative or over-cap count, refuse a count whose bytes are not present, skip a payload that is present but over-cap (catchup and spectator batches read several commands from one reader, so an unread payload misaligns the rest of the batch), and expose the verdict as an `IsValid` flag. Commands are pooled and only `PlayerId`/`Tick` are reset on rent, so every field the deserializer owns must be reassigned on every pass.
2. **Domain**, in [`CommandValidation.Accept`](Commands/CommandValidation.cs), called once from `CommandSystem.OnCommand`. Checks that each field names something that can exist — a coordinate inside the world envelope, an asset id the registry knows. Handlers then spend their own checks on game state (ownership, gold, range), which is where those belong.

Both layers run inside the simulation so client prediction and the authoritative server reach the same verdict for the same frame. Validating on the server ingest path instead would accept a command locally that the server discarded, and the client would mispredict every time.

Wire limits live in [`CommandLimits`](Commands/CommandLimits.cs), not `Assets.json`: they must be identical on both sides and stable across recorded replays. `MaxSelectedUnits` is derived from the unreliable-datagram budget, since LiteNetLib throws rather than fragment an unreliable packet — an oversized selection would crash the sending client too.

# Commands

- Sim tests from repo root: `just test`
