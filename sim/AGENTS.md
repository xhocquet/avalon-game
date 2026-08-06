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
  - `Defense` → `Stats.Defense`, mitigating a fraction of each incoming hit (`damage * 100 / (100 + Defense)`) so stacking approaches but never reaches immunity. Every unit type carries its own base value.
  - `AttackReacquireRangeMultiplier` → `Combat.AttackReacquireRangeMultiplier`; `TargetAcquisitionSystem` scales `Combat.AttackRange` by it to get the acquisition radius. Each unit type sources its own — turrets sit at 1.0 because they cannot chase.
  - `AttackCooldownTicks` is the **base period** and stays put in `Combat`; `Stats.AttackSpeed` is the multiplier, and `DamageSystem` divides at the moment of the hit so bonuses stay additive on the rate and rounding never compounds.
- The spawned entity stores `Hero.HeroAssetId`, so any system can get back to the row without going through the faction.
- Hero-specific *code* lives in an [`IHeroBehavior`](Heroes/IHeroBehavior.cs) selected by `HeroAsset.BehaviorId` through [`HeroBehaviors.Get`](Heroes/HeroBehaviors.cs). `HeroFactory` calls `OnSpawn`; [`HeroBehaviorSystem`](Systems/HeroBehaviorSystem.cs) calls `OnTick` for every hero each tick.
- Behaviors are **stateless singletons**. Components are the only rollback-safe storage — a field on a behavior survives a rollback and desyncs the client. Add a component in `OnSpawn` and mutate it in `OnTick`.
- Components can't be subclassed (they are `[StructLayout(Sequential)]` structs snapshot by value), and adding a hero must never change the component layout heroes share.
- Adding a hero: allocate an id in `AssetIds`, add the row to `Assets.json`, regenerate `Assets.bytes`, point a `FactionAsset` at it. Code is only needed when it wants behavior no existing `BehaviorId` covers.

# XP & Leveling

- Progression lives on [`ExperienceComponent`](Components/Behaviors/ExperienceComponent.cs) (Level, lifetime Experience), added to every hero by `HeroFactory`. It is deliberately not on `StatsComponent` — Stats holds buffable combat values that skills and items write through `Stats.Add`; XP is the input that *produces* those writes.
- All numbers live in [`XpRulesAsset`](Assets/XpRulesAsset.cs) (row `AssetId: 115`): per-victim-type kill rates, the level curve, and the per-level stat gains. Rates are flat across players; a kill is worth what the victim is worth.
- The curve is an arithmetic series in integers — level 2 costs `XpToSecondLevel`, each level after costs `XpPerLevelIncrement` more. `XpRulesAsset.TotalXpForLevel(level)` is the closed form and is what UI should use for a progress bar.
- **Awarding** happens at the kill site through [`ExperienceRewards.AwardForKill`](ExperienceRewards.cs), called from `DeathSystem` (everything on the board) and `RespawnSystem.BeginRespawn` (heroes, which never reach `DeathSystem`). Credit goes to **whoever landed the fatal hit** (`Health.LastDamagerUnitId`, recorded by `DamageSystem`) and nowhere else — no team split, no proximity share. Only heroes carry an `ExperienceComponent`, so a kill credited to a minion or turret pays out nothing. Friendly fire and unattributed deaths award nothing.
- `DeathSystem` resolves the killer once per corpse into `DeadUnitSnapshot` (entity + its `UnitIdComponent` + team/owner) and pays every award out in a pass **before** the destroy pass, so a killer that dies on the same tick still earns what it killed. `UnitDiedEvent` carries `DestroyerUnitId`/`DestroyerUnitTypeId` for the view.
- **Spending** happens in [`ExperienceSystem`](Systems/ExperienceSystem.cs), registered right after `DeathSystem` so a kill lands its level on the same tick. It is the only writer of `Level`, applies gains via `Stats.Add`, tops current HP up by the MaxHealth delta (skipping a hero at 0 HP awaiting respawn), and raises one `HeroLeveledUpEvent` carrying the level reached even when several levels land at once.
- XP is lifetime-earned and never reset, so it survives death and respawn for free — the hero entity is never destroyed.

# Skills & Upgrades

- Every hero has four skill slots — `HardHit`, `Buff`, `RangeShot`, `Ultimate` ([`SkillSlot`](Enums.cs)) — each ranked 0-4. The slot indices are the indices into `SkillsComponent`'s fixed buffers and into `HeroAsset.Skill1..4AssetId`, so they must stay 0-based and contiguous.
- Numbers live in [`SkillAsset`](Assets/SkillAsset.cs), four rows per hero in the `AssetIds.Skill*` block (500-519). Every hero owns its own rows even where they currently match, so retuning one hero's skill never touches another's. `CooldownMs` is authored in milliseconds; `SkillActions.CooldownTicks` converts it once, at cast time, with the same ceiling-divide `RespawnSystem` uses.
- State lives on [`SkillsComponent`](Components/Behaviors/SkillsComponent.cs): unspent points, the four `SkillAsset` ids, ranks, and cooldowns, all as `fixed int` buffers (52B). `HeroFactory` copies the ids off `HeroAsset` at spawn, so nothing downstream reaches the asset registry to find out which skills a hero owns.
- **Earning**: a hero spawns with 1 point (level 1 counts as a level) and `ExperienceSystem` grants one per level gained, so points always equal level. Points are not a stat and do not route through `Stats.Add` — the tree spends them itself.
- **Spending and casting** both go through [`SkillActions`](Heroes/SkillActions.cs), dispatched from `CommandSystem` by `UpgradeSkillCommand` / `CastSkillCommand`. `CommandValidation` range-checks the slot first, and it is the only thing standing between a wire value and an unchecked fixed-buffer index. A cast starts its cooldown *before* running the effect, so an effect that kills or respawns its own caster cannot leave the slot free.
- [`SkillSystem`](Systems/SkillSystem.cs) only burns cooldowns down, registered before `HeroBehaviorSystem` so skill state is current when behaviors tick. A cast on tick N loses one tick to it on that same tick, because commands are delivered before the Update phase — the same behaviour `AttackCooldownSystem` has, identical on both peers. It is not an off-by-one.
- Hero-specific skill *code* lives in an [`IHeroSkillSet`](Heroes/IHeroSkillSet.cs) selected by `HeroAsset.SkillSetId` through [`HeroSkillSets.Get`](Heroes/HeroSkillSets.cs) — one folder per hero under `Heroes/` (e.g. `Heroes/Shroom/ShroomSkills.cs`), each set owning all four of that hero's slots, while the shared plumbing sits at the root of `Heroes/`. Everything under `Heroes/` is namespace `...Sim.Heroes`; the per-hero folder holds what that hero owns, it is not a namespace boundary. This is deliberately separate from `BehaviorId`: that selects spawn/tick logic and is 0 for every hero, while skills are per-hero from the start. Like behaviors, skill sets are **stateless singletons**.
- Effects are currently stubbed in `SharedSkillStubs`. Implementing one means replacing a single delegation in one hero's file; nothing in `SharedSkillStubs` is meant to survive as shared behaviour.
- Not built yet: targeting (a cast carries only a slot), cast time, resource costs, respec, and rank prerequisites. A level gate would be a `MinHeroLevel` field on `SkillAsset` plus one rung in `SkillActions.TryUpgrade`.

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

- Prefer compact intent commands with stable `UnitIdComponent.UnitId` references. Do not put transient ECS entity ids in command payloads.
- Movement is planar: `TransformComponent.Position.x/z`.
- NO dynamic physics. Use deterministic transform integration, radii, proximity queries, grids, and stable iteration order.
- When changing gameplay rules, inspect `sim/` first instead of duplicating logic in `client/` or `server/`.
- Klotho asset id ranges (both the AssetId and wire TypeId planes) are tracked in `sim/Assets/AssetIds.cs`; allocate from the "next free" markers there.
- Systems hold no tuning constants. Gameplay numbers live in `client/Sim/Data/Assets.json` and are read through `frame.AssetRegistry.Get<T>()`; after editing the JSON run `just` asset generation (`dotnet run --project tools/AssetGen`) to rebuild `Assets.bytes`.

# Ownership

Two id spaces, and they do not mix:

- **`TeamComponent.TeamId`** is the sim's answer to every "may this actor do this" question. Control (`UnitLookup.TryGetPlayerControllableUnitById`), hostility (`CombatTargeting.IsHostileAndAlive`), attack-order targeting, shop proximity, and kill credit all resolve through team. A player reaches their units via `Hero.PlayerId` → `TryGetPlayerTeamId` → team, so player-scoped rules never need a second owner field.
- **`OwnerComponent.OwnerId` is a player id, and belongs only on the hero.** It is a Klotho built-in that the *view* layer reads as a player id: `EntityViewFactory.TryGetBindBehaviour`/`GetViewFlags` compare it against `Engine.LocalPlayerId` to choose predicted vs. verified render, and `EntityViewUpdaterNode` keys `PlayerViewRegistry` off it. Putting a team id there makes team 1's units render off the predicted frame for player 1 while team 2's interpolate — the ids happen to both number from 1, so it looks correct and silently isn't. Minions, crystals, and turrets carry no `OwnerComponent`; `OwnersMatch` short-circuits to true for entities without one, so their views need no `OwnerMatches` override.

# Command Validation

Command payloads come off the wire from untrusted peers, and nothing between the socket and the simulation catches an exception. The path runs through Klotho's networking layer in `vendor/Klotho` (not the project's own `server/`): LiteNetLib's `ProcessEvent`, `ServerNetworkService.HandleClientInputMessage`, and `ServerLoop.ExecuteCycle` all let one propagate, and `ServerLoop.Run` wraps the loop in `try`/`finally` with no `catch`. A command that throws or corrupts state while deserializing takes the server process down with every room on it, so validation is not optional for a new command type.

Every command passes through two layers before a handler runs:

1. **Structural**, inside the command's own `DeserializeData`. A variable-length field must never size a buffer or advance the reader from an unchecked wire count. See [`UnitIdList.Deserialize`](Commands/UnitIdList.cs) for the pattern: reject a negative or over-cap count, refuse a count whose bytes are not present, skip a payload that is present but over-cap (catchup and spectator batches read several commands from one reader, so an unread payload misaligns the rest of the batch), and expose the verdict as an `IsValid` flag. Commands are pooled and only `PlayerId`/`Tick` are reset on rent, so every field the deserializer owns must be reassigned on every pass.
2. **Domain**, in [`CommandValidation.Accept`](Commands/CommandValidation.cs), called once from `CommandSystem.OnCommand`. Checks that each field names something that can exist — a coordinate inside the world envelope, an asset id the registry knows. Handlers then spend their own checks on game state (ownership, gold, range), which is where those belong.

Both layers run inside the simulation so client prediction and the authoritative server reach the same verdict for the same frame. Validating on the server ingest path instead would accept a command locally that the server discarded, and the client would mispredict every time.

Wire limits live in [`CommandLimits`](Commands/CommandLimits.cs), not `Assets.json`: they must be identical on both sides and stable across recorded replays. `MaxSelectedUnits` is derived from the unreliable-datagram budget, since LiteNetLib throws rather than fragment an unreliable packet — an oversized selection would crash the sending client too.

# Commands

- Sim tests from repo root: `just test`
