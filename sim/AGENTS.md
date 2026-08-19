# Contents

Deep dives in [`docs/`](docs/):

- [Heroes](docs/heroes.md) — `HeroAsset` fields, combat range/timing, `BehaviorId`, adding a hero
- [XP & Leveling](docs/xp-and-leveling.md) — `ExperienceComponent`, level/stat-growth curves, kill awards
- [Skills & Upgrades](docs/skills-and-upgrades.md) — slots, `SkillAsset` tuning, casting/targeting, effect lifecycles
- [Match End & Results](docs/match-end-and-results.md) — win conditions, `MatchOutcome`, per-player stats, `MatchRecord`
- [Navigation](docs/navigation.md) — agent phases, temporal spreading, navmesh baking, move-target resolution, flow fields

In this file: [Gold](#gold) · [Stats](#stats) · [Filter Iteration](#filter-iteration) · [Working Rules](#working-rules) · [Ownership](#ownership) · [Command Handling](#command-handling) · [Command Validation](#command-validation) · [Test Cheats](#test-cheats) · [Repo Commands](#repo-commands)

`sim/` is authoritative deterministic gameplay compiled into both client and server — read [Shared Simulation](../AGENTS.md#shared-simulation) in the root doc before editing here.

# Gold

- [`InventoryComponent`](Components/Behaviors/InventoryComponent.cs) is the wallet — integer `Gold`, the accrual rate `GoldPerTick`, the purchased-item ledger. Only heroes carry one.
- Pacing is on `MatchRulesAsset` (asset 110): `StartingGold` seeds the wallet in `HeroFactory`, `GoldStartDelayMs` gates the trickle, `GoldTickIntervalMs`/`StartingGoldPerTick` are its rate.
- [`InventorySystem`](Systems/InventorySystem.cs) gates on `frame.Tick`, so a hero spawning late gets no private delay, and `GoldAccrualRemainderMs` doesn't bank before the gate opens — the first payout lands one full interval after it.
- Bounties are per-victim-type on [`GoldRulesAsset`](Assets/GoldRulesAsset.cs) (asset 118). [`GoldRewards.AwardForKill`](GoldRewards.cs) mirrors `ExperienceRewards`: same call sites, same `MatchStats.IsCreditableKill` gate, fatal hit only.
- `StartingGold` is granted once at spawn; the wallet survives death because the hero entity is never destroyed.
- `GoldPerAssist` is unread.

# Stats

- [`StatsComponent`](Components/Behaviors/StatsComponent.cs) is one `FP64` per [`StatType`](Enums.cs) in a `fixed long` buffer of raw 32.32 values rather than named fields — a stat can't be missed in a `switch`, every write goes through the same clamp, and fractional modifiers survive instead of truncating. `readonly` properties keep call sites reading `stats.MoveSpeed`.
- `StatType` values stay contiguous from 0, and [`StatRanges.Rows`](StatRanges.cs) carries one row per entry in the same order. Nothing serializes the enum, so renumbering is safe.
- `StatRanges` holds `(Min, Max, Initial)` per stat in code rather than asset JSON: these are the bounds that stop a divide by zero or an empty health pool, the same class of thing as `CommandLimits`. Tuning goes in the asset row, inside them.
- `StatsComponent.Create()` is the only correct starting point — a default-constructed block is all zeroes, out of range for any stat with a non-zero floor. `From(IUnitStatsAsset)` builds on it, and every factory goes through one of the two.
- [`DamageApplication.Mitigate`](DamageApplication.cs) scales a hit by `100 / (100 + resist)`, mirrored for a negative resist, floored at 1 damage.
- [`HealthApplication`](HealthApplication.cs) is the only writer of `Health.Current` upward: `ApplyHeal` clamps to `Stats.MaxHealth` and refuses a unit at 0, `RestoreToFull` is the respawn path that skips that check, `GrantMaxHealth` moves pool and current together.
- Damage is `FP64` end to end. Rounding is at the edges only: `MatchResult` for the scoreboard JSON, `.ToFloat()` in the view.
- Gold accrual is not a stat — see [Gold](#gold).

# Filter Iteration

**Inside a `frame.Filter<...>` loop, never add or remove any of that filter's own component types — on any entity, including the one the filter just handed you.** Collect the entities into a system-owned `List<EntityRef>` and apply the change after the loop. Components outside the filter's type list are free to add and remove inline, and so is `DestroyEntity` on an entity no live filter is walking.

A `Filter` is a `ref struct` that snapshots `Count` and holds a span over the live dense array of *one* of its storages — the smallest, chosen at construction, so which one is invisible at the call site. `ComponentStorageFlat.Remove` compacts by swapping the last dense slot into the removed one, which moves an unvisited entity into a slot already passed. The failure is an entity visited twice in one pass, or — for the single-type `Filter<T1>`, which has no `Has` re-check — a removed entity visited and its stale component read. Both are silent and both desync a rollback replay.

Removing the current entity's own component survives by accident of layout: the stale dense tail past the new `Count` still resolves through `Has()`. The rule covers it anyway. `CommandSystem`, `AttackIntentSystem`, `OasisSpawnSystem`, `DeathSystem`, `ProjectileSystem`, `PickupSystem`, and `TeamPruneSystem` all defer.

# Working Rules

- Prefer compact intent commands with stable `UnitIdComponent.UnitId` references. Do not put transient ECS entity ids in command payloads.
- Movement is planar: `TransformComponent.Position.x/z`.
- NO dynamic physics. Use deterministic transform integration, radii, proximity queries, grids, and stable iteration order.
- Changing a gameplay rule starts in `sim/`, not a duplicate in `client/` or `server/`.
- Klotho asset id ranges (both the AssetId and wire TypeId planes) are tracked in [`Assets/AssetIds.cs`](Assets/AssetIds.cs); allocate from the "next free" markers there.
- Randomness is derived, never carried. Every `DeterministicRandom` stream comes from `SimRandom.WorldSeed` plus a feature key allocated in [`SimRandom`](SimRandom.cs) and an index built from frame data — `OasisSpawnSystem` uses oasis id + tick, `CriticalStrikes` attacker unit id + tick. A stream that remembered where it left off would leak the discarded prediction branch into the replay.
- Systems hold no tuning constants. Gameplay numbers live in `client/Sim/Data/Assets/` — one `*.json` array per topic, per-hero files under `heroes/` — and are read through `frame.AssetRegistry.Get<T>()`. `AssetGen` merges every `*.json` under that directory recursively, so a new file needs no index entry; after editing run `dotnet run --project tools/AssetGen` to rebuild `Assets.bytes`. It fails on a duplicate `AssetId` or a row without one.

# Ownership

Two id spaces, and they do not mix:

- **`TeamComponent.TeamId`** answers every "may this actor do this" question — control, hostility, attack-order targeting, shop proximity, kill credit. A player reaches their units via `Hero.PlayerId` → `UnitLookup.TryGetPlayerTeamId` → team, so player-scoped rules never need a second owner field.
- **`OwnerComponent.OwnerId` is a player id, and belongs only on the hero.** The view layer reads it as one: `EntityViewFactory.TryGetBindBehaviour`/`GetViewFlags` compare it against `Engine.LocalPlayerId` to choose predicted vs. verified render, and `EntityViewUpdaterNode` keys `PlayerViewRegistry` off it. A team id there makes team 1's units render off the predicted frame for player 1 while team 2's interpolate — both spaces number from 1, so it looks correct and silently isn't. Minions, crystals, and turrets carry no `OwnerComponent`; `OwnersMatch` short-circuits to true without one, so their views need no `OwnerMatches` override.

# Command Handling

- [`CommandSystem.OnCommand`](Systems/CommandSystem.cs) runs [`CommandValidation.Accept`](Commands/CommandValidation.cs), then delegates to a static `*Actions` class — [`SkillActions`](Heroes/SkillActions.cs), [`ShopActions`](ShopActions.cs), [`FactionActions`](FactionActions.cs). Each exposes a `Try*` returning `bool` and funnels every bailout through one private `Reject` logger, so a rejection is one line with a `reason=`. Move and attack orders stay inline because they own the selection/formation plumbing.
- **Where `CommandSystem` sits in `RegisterSystems` has nothing to do with command intake.** `EcsSimulation.Tick` drains every `OnCommand` before it calls `RunUpdateSystems`. That slot governs only `CommandSystem.Update`, the transform integrator for whatever `NavigationAgentSystem` will not carry: every unit when `navigation` is null, otherwise just move targets held by non-nav agents.
- [`UnitIntent`](UnitIntent.cs) is the only writer of `UnitMoveTarget` and `AttackTargetUnitId`. `SetMoveTarget` flattens `y` itself — a structure's target comes off a map marker that carries a height. `SetAttackTarget` writes the order alone; `AttackIntentSystem` resolves it into `Combat.TargetUnitId`. `ClearAttackIntent` drops both.
- A `UnitLookup.Index` field on a system is storage, not state. Rebuild it at the entry point and pass it as a parameter; an index left on the field for a later method to find outlives its command, survives a rollback, and resolves ids against a frame that no longer exists.
- The client-facing `Can*` predicate the root doc's [shared-simulation rule](../AGENTS.md#shared-simulation) requires is worked out in [`SkillActions`](Heroes/SkillActions.cs): `EvaluateCast`/`EvaluateUpgrade` return a `SkillBlock` code, `TryCast`/`TryUpgrade` turn a block into the `reason=` log, `CanCast`/`CanUpgrade` compare against `None`. Render the reason text only on the reject path.
- `ShopActions.IsHeroNearTeamShop` is the single range rule for shops, and the client calls it rather than measuring against the selected `ShopEntity` node. The node's transform comes from `World.tscn` and the sim's from the `MapLayoutAsset` Shop marker; wherever those drift, measuring against the node enables a button the sim then rejects with nothing but a log line.
- Gameplay logging goes through [`SimLog`](SimLog.cs), never a raw `frame.Logger` call. A server-driven client replays its whole predicted window on each verified batch, so an unguarded line reappears a dozen times per event; `SimLog` binds to the engine stage and stays quiet on replayed ticks.

# Command Validation

Command payloads come off the wire from untrusted peers, and nothing between the socket and the simulation catches an exception. The path runs through Klotho's networking layer in `vendor/Klotho`, not the project's own `server/`: LiteNetLib's `ProcessEvent`, `ServerNetworkService.HandleClientInputMessage`, and `ServerLoop.ExecuteCycle` all let one propagate, and `ServerLoop.Run` wraps the loop in `try`/`finally` with no `catch`. A command that throws or corrupts state while deserializing takes the server process down with every room on it.

Every command passes two layers before a handler runs:

1. **Structural**, inside the command's own `DeserializeData`. A variable-length field must never size a buffer or advance the reader from an unchecked wire count. [`UnitIdList.Deserialize`](Commands/UnitIdList.cs) is the pattern: reject a negative or over-cap count, refuse a count whose bytes are not present, skip a payload that is present but over-cap (catchup and spectator batches read several commands from one reader, so an unread payload misaligns the rest of the batch), and expose the verdict as an `IsValid` flag. Commands are pooled and only `PlayerId`/`Tick` are reset on rent, so every field the deserializer owns must be reassigned on every pass.
2. **Domain**, in [`CommandValidation.Accept`](Commands/CommandValidation.cs), called once from `CommandSystem.OnCommand`. Checks that each field names something that can exist — a coordinate inside the world envelope, an asset id the registry knows. Handlers then spend their own checks on game state (ownership, gold, range).

Both layers run inside the simulation so client prediction and the authoritative server reach the same verdict for the same frame. Validating on the server ingest path instead would accept a command locally that the server discarded, and the client would mispredict every time.

Wire limits live in [`CommandLimits`](Commands/CommandLimits.cs), not the assets: they must be identical on both sides and stable across recorded replays. `MaxSelectedUnits` is derived from the unreliable-datagram budget, since LiteNetLib throws rather than fragment an unreliable packet — an oversized selection would crash the sending client too.

# Test Cheats

`--godmode` on the client command line makes that player's hero take no damage. It is parsed by [`CheatOptions`](../client/Scripts/View/CheatOptions.cs), sent as `SetCheatCommand`, and stored per player in the `CheatState` singleton, which [`Cheats`](Cheats.cs) reads and `DamageApplication` gates on. Nothing authorizes the command beyond scoping it to the issuing player.

Adding another cheat: a value in [`CheatFlags`](Enums.cs), the same bit in `Cheats.All` so validation accepts it, the arg in `CheatOptions`, and the read wherever the rule lives. The command and the storage need no change.

`godmode` is a justfile variable, so the assignment goes **before** the recipe name — `just godmode=true quickplay 1000 202 201`. After the recipe name it would be read as a positional argument. `play`, `quickplay`, and `smoke` all pass it to both clients.

# Repo Commands

- Sim tests from repo root: `just test`
