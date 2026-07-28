
## Tools
- [`SimMarkerNode`](client/Scripts/SimMarkerNode.cs) - Markers can be placed in Godot and used in sim code (spawns, shops)
  - These are baked with Godot tool [`GodotFPMapLayoutExporter`](client/Scripts/Editor/GodotFPMapLayoutExporter.cs) and saved to [`Sim/Data/MapLayout.bytes`](client/Sim/Data/MapLayout.bytes)
- In the same way, we generate a deterministic navmesh to [`NavigationRegion3D.NavMeshData.bytes`](client/Sim/Data/NavigationRegion3D.NavMeshData.bytes)
- [`UnitLookup`](sim/UnitLookup.cs) provides stable identifiers for all units, and resolves them back to entities

## Todo

| Status | Work |
| ------ | ------------------------------------------------------------------------------------------------------------- |
| ⬜ | Add event-driven audio reactions from synced events, not gameplay authority. |
| ⬜ | Add model/team tinting for structure views. |
| ⬜ | Multi-threaded A* — leverage 6-8 cores for parallel path queries. |

## Code Review: `sim/`

Review of all hand-written code under [`sim/`](sim) (excluding `Tools/Generated/`), covering code conventions, style consistency, defensive code, deterministic math, and code organization.

### Determinism

The obvious axis is clean: **zero** `float`, `double`, `Math.`, `DateTime`, `Random`, or LINQ anywhere in `sim/`. RNG is derived purely from `(worldSeed, featureKey, oasisId, tick)` ([`OasisSpawnSystem.cs:106-112`](sim/Systems/OasisSpawnSystem.cs)), ties break on `UnitId` ([`TargetAcquisitionSystem.cs:101`](sim/Systems/TargetAcquisitionSystem.cs), [`GroupFormation.cs:53`](sim/Navigation/GroupFormation.cs)), and `GetAuthoredTeamIds` sorts before use. [`SpatialHashGrid`](sim/Navigation/SpatialHashGrid.cs) keys cells by exact coordinate instead of enumerating the dictionary — deliberately, and documented.

The problem is one axis nobody checked: **state that lives on a system instead of in the frame.**

**[`NavigationAgentSystem._lastSnappedPositions`](sim/Systems/NavigationAgentSystem.cs) (`:35`, written at `:314`) is a rollback divergence vector.**

```csharp
// NavigationAgentSystem.cs:298-309
var delta = position - _lastSnappedPositions[slotIndex];
var moveSqr = delta.x * delta.x + delta.z * delta.z;
if (nav.CurrentTriangleIndex >= 0 && moveSqr < snapThresholdSqr) {
  nav.Position = position;   // skip the navmesh snap
  return;
}
var snapXZ = _navigation.Query.ClosestPointOnNavMesh(...);  // …or snap, changing nav.Position
```

The branch taken decides whether `nav.Position` gets snapped, which feeds `transform.Position` at `:166`. The array is a plain field on the system — it is not frame state, so it does not roll back. A client that mispredicts and resimulates ticks T..T+n carries `_lastSnappedPositions` from the *mispredicted* run into the replay; the server never had those values. Different branch, different position.

The codebase already knows this rule and states it twice:

- [`MatchSetupState.cs:7`](sim/Components/MatchSetupState.cs) — *"Stored in frame state (not on the system) so it rolls back deterministically"*
- [`UnitLookup.cs:89`](sim/UnitLookup.cs) — *"never cached across ticks: a stale index survives a rollback and resolves against a frame that no longer exists"*

`NavigationAgentSystem` is the one system that breaks it. The other caches (`_heroAvoidanceGrid`, `_minionAvoidanceGrid`, `_candidateGrid`) are cleared and rebuilt every tick, so they're fine; [`FlowFieldCache`](sim/Navigation/FlowFieldCache.cs) derives from the static navmesh, also fine. Fix is to move the last-snapped position onto the nav agent (or a small component) so it rides the snapshot.

Note the determinism baseline test won't catch this — running the sim twice from scratch produces identical `_lastSnappedPositions` both times. It only shows up under rollback.

**The same array is indexed by iteration slot, not by entity.**

`SyncAgentPosition(ref nav, transform.Position, _allCount, snapThresholdSqr)` at `:73` passes the running collection counter as the slot. Slot 3 is whichever agent happened to be 4th in filter order this tick. Spawn a minion, kill a hero, and slot 3 is a different unit — so the "has this agent moved far enough to re-snap?" test compares agent A's position against agent B's last snap. When it wrongly decides *not* to snap, `nav.CurrentTriangleIndex` is left stale while `nav.Position` takes the raw transform, which is how an agent drifts off-mesh with a bad triangle index. This is deterministic (both sides compute the same wrong answer), so it's a correctness bug rather than a desync — but it compounds the one above.

### Defensive code

**[`MapLayoutAsset.TryGetByTypeAndTeam`](sim/Assets/MapLayoutAsset.cs) (`:19-22`) trusts the parallel-array invariant it can't see.** It null-checks `MarkerTypes`, then indexes `MarkerTeams[i]` and `MarkerPositions[i]` with the same `i`. A short or null companion array from a bad `Assets.json` throws deep inside world init. [`SimulationSetup.SpawnPickups:156`](sim/SimulationSetup.cs) does the right thing for the fourth array (`MarkerValues != null && i < MarkerValues.Length`) — the asset should enforce that for all four, once, rather than each caller remembering.

**Untrusted command payloads have no bound check.** [`MoveCommand.DeserializeData:44`](sim/Commands/MoveCommand.cs) and [`AttackCommand:41`](sim/Commands/AttackCommand.cs) read an `Int16` count off the wire and size an array from it with no cap. Complementary hole on the write side: `AddUnitId` grows unboxed past `short.MaxValue`, at which point `(short)UnitIdCount` truncates in `SerializeData` while `GetSerializedSize` returns the untruncated length — the two disagree about the frame size. A selection-size cap enforced in both `AddUnitId` and `DeserializeData` closes both ends.

**[`ScoreSystem.IsTimeoutTick`](sim/Systems/ScoreSystem.cs) (`:49-51`) is fragile twice over:**

```csharp
var matchEndTick = matchDurationMs / frame.DeltaTimeMs;   // unguarded divide
return frame.Tick == matchEndTick;                        // exact equality
```

[`RespawnSystem:109`](sim/Systems/RespawnSystem.cs) guards the identical division (`frame.DeltaTimeMs > 0 ? frame.DeltaTimeMs : 16`); `ScoreSystem` doesn't. And exact `==` means a single missed evaluation on that tick leaves the match running forever — `>=` costs nothing and can't miss.

**[`GroupFormation.GetForward:66`](sim/Navigation/GroupFormation.cs)** divides by `units.Count` with no zero guard. Safe today only because [`CommandSystem:246`](sim/Systems/CommandSystem.cs) returns early when the count is 1 or 0.

### Convention violations

[`sim/AGENTS.md`](sim/AGENTS.md) states: *"Systems hold no tuning constants. Gameplay numbers live in `client/Sim/Data/Assets.json`."* Five places don't:

| Location | Constant |
| -------- | -------- |
| [`TargetAcquisitionSystem.cs:127`](sim/Systems/TargetAcquisitionSystem.cs) | `FP64.FromInt(3)` reacquire-range fallback |
| [`NavAgentFactory.cs:11`](sim/Factories/NavAgentFactory.cs) | `speed * FP64.FromInt(12)` acceleration |
| [`NavigationRuntime.cs:37`](sim/Navigation/NavigationRuntime.cs) | `avoidance.TimeHorizon = FP64.FromInt(2)` |
| [`WaveSpawnSystem.cs:83`](sim/Systems/WaveSpawnSystem.cs) | `spacing * FP64.FromInt(2)` spawn-cluster offset |
| [`RespawnSystem.cs:44`](sim/Systems/RespawnSystem.cs) | `player.Score -= 1` death penalty |

The ORCA time horizon is the sharpest one: it has a four-line comment explaining the tuning rationale in exactly the register [`NavigationTuningAsset`](sim/Assets/NavigationTuningAsset.cs) was built for, and it sits in code instead. `TargetAcquisitionSystem`'s `FP64.FromInt(3)` is a fallback that silently masks a missing asset row — the surrounding code style would be `if (stats == null) return;`.

[`WaveSpawnSystem.GetSpawnPosition:78`](sim/Systems/WaveSpawnSystem.cs) has a subtler one — a hardcoded assumption rather than a number:

```csharp
var forward = new FPVector3(-origin.x, FP64.Zero, -origin.z);
```

"Toward the lane" is defined as "toward world origin." That's true for the current symmetric map and silently wrong for any map whose center isn't (0,0). It belongs in [`MapLayoutAsset`](sim/Assets/MapLayoutAsset.cs) next to the spawn marker.

### Style consistency

Most of this is cosmetic, but it's the kind that accumulates:

- **Namespace vs. folder.** Every file in [`sim/Systems/`](sim/Systems) declares `namespace Meesles.Avalon`, while everything else in `sim/` uses `Meesles.Avalon.Sim`, `.Sim.Components`, `.Sim.Navigation`, etc. Systems are the only directory whose namespace doesn't track its path.
- **[`TriangleFlowField.AT_GOAL` / `UNREACHABLE`](sim/Navigation/TriangleFlowField.cs) (`:7-8`)** are the only SCREAMING_CASE identifiers in the project; every other constant is PascalCase (`NoWinnerPlayerId`, `RandomFeatureKey`, `FirstUnitId`, `MaxItems`).
- **[`SimulationSetup.cs:84`](sim/SimulationSetup.cs)** takes `Boolean spawnHeroesNow` — the only BCL alias in `sim/`.
- **[`Enums.cs`](sim/Enums.cs)** bundles three unrelated enums into a catch-all while every other type gets its own file.
- **[`TargetAcquisitionSystem.cs:85`](sim/Systems/TargetAcquisitionSystem.cs)** compares `candidate.Index == attacker.Index`; [`DeathSystem.cs:92`](sim/Systems/DeathSystem.cs) compares full `EntityRef` equality (`combat.Target != deadEntity`). `EntityRef` is `(Index, Version)` and implements `IEquatable`. The Index-only form is correct within a tick but is the weaker habit.
- **[`NavigationAgentSystem` field order](sim/Systems/NavigationAgentSystem.cs) (`:16-40`)** looks alphabetized by tooling — `_heroCount` sits between the grids and the hero arrays, `_minionCount` between `_lastSnappedPositions` and `_minionEntities`, splitting comment blocks from what they document.
- **[`MinionStatsAsset`](sim/Assets/MinionStatsAsset.cs) is also the hero combat asset** ([`HeroFactory.cs:9`](sim/Factories/HeroFactory.cs) says so explicitly, and [`Combat`](sim/Components/Combat.cs)'s primary constructor at `:12` takes `MinionStatsAsset`). Meanwhile [`TurretFactory:26`](sim/Factories/TurretFactory.cs) can't use that constructor and hand-rolls the object initializer. The type is really `CombatStatsAsset`.

### Organization & duplication

**Four capacity helpers where one already exists.** [`NavigationAgentSystem`](sim/Systems/NavigationAgentSystem.cs) has a correct generic `EnsureCapacity(ref EntityRef[], int)` at `:351` — and then `EnsureHeroCapacity` (`:359`), `EnsureMinionCapacity` (`:367`), and `EnsureAllCapacity` (`:324`) reimplement the identical doubling loop. Only `EnsureAllCapacity` justifies itself (it resizes a parallel array); the other two are pure copy-paste.

**Set-or-add-`UnitMoveTarget` appears three times**, character-for-character: [`CommandSystem.SetAttackMoveTarget:219`](sim/Systems/CommandSystem.cs), [`CommandSystem.SetTarget:293`](sim/Systems/CommandSystem.cs), [`AttackIntentSystem.SetMoveTarget:107`](sim/Systems/AttackIntentSystem.cs). The clear-`Combat.Target` idiom is likewise duplicated across `CommandSystem:287`, `AttackIntentSystem:101`, and [`RespawnSystem:85`](sim/Systems/RespawnSystem.cs). These are the ECS equivalent of a setter — one shared `UnitIntent` helper class would cover both.

**[`MoveCommand`](sim/Commands/MoveCommand.cs) and [`AttackCommand`](sim/Commands/AttackCommand.cs) are structurally identical** — same growable `int[]`, same `Add`/`Get`, same count-prefixed serialization — with no shared base.

**Klotho's `FilterWithout<>` is never used.** Three systems hand-roll exclusions the API supports directly: [`DeathSystem:16-18`](sim/Systems/DeathSystem.cs) (`Filter<Unit, Health>` + skip `Player`), [`NavigationAgentSystem:61-66`](sim/Systems/NavigationAgentSystem.cs) (skip `PendingRespawn`), [`TargetAcquisitionSystem:55-59`](sim/Systems/TargetAcquisitionSystem.cs).

**Two hot paths do full O(n²) scans while a spatial grid sits unused next door.** `TargetAcquisitionSystem` correctly buckets candidates into a [`SpatialHashGrid`](sim/Navigation/SpatialHashGrid.cs) — but:

- [`WaveSpawnSystem.GetFirstFreeSlot:46`](sim/Systems/WaveSpawnSystem.cs) probes slots in an unbounded `while` loop, and each probe (`IsSlotOccupied:54`) scans *every minion on the map* for that team. That's O(slots × minions) per wave-spawn tick, growing quadratically with wave size.
- [`PickupSystem:22-27`](sim/Systems/PickupSystem.cs) nests `Filter<Inventory, TransformComponent>` inside `Filter<Pickup, TransformComponent>` — O(pickups × collectors) every tick.

Given the memory note about minion counts, `WaveSpawnSystem` is the one that will bite first.

**[`DeathSystem.ResolveDestroyerContext:87-101`](sim/Systems/DeathSystem.cs) is both slow and wrong.** It runs a full `Filter<Unit, Combat>` scan *per dead unit*, and then attributes the kill to whichever attacker currently targeting the corpse has the **lowest `UnitId`** — not the one that landed the killing blow. [`DamageSystem:32`](sim/Systems/DamageSystem.cs) knows exactly who dealt the fatal damage and throws that away. Recording the last damager on the [`Health`](sim/Components/Health.cs) component would make attribution correct and delete the scan.

### Dead code

- **[`Stats.Defense` and `Stats.Speed`](sim/Components/Stats.cs) (`:9-10`)** are never read anywhere in `sim/`, `client/`, `server/`, or `tests/`. They default to 100 and ride every rollback snapshot.
- **[`FlowFieldCache.Version` and `Invalidate()`](sim/Navigation/FlowFieldCache.cs)** are never called — meaning flow fields are never invalidated. Harmless while the navmesh is static, but the API implies otherwise.
- **[`TriangleFlowField.Cost` and `GoalTriangleIndex`](sim/Navigation/TriangleFlowField.cs) (`:9`, `:12`)** are written in the constructor and never read. `Cost` is a `FP64[triCount]` retained for the lifetime of every cached field.
- **[`Pickup.Type`](sim/Components/Pickup.cs)** is a commented-out field with a TODO.

### Stale comments

- **[`PendingRespawn.cs:6`](sim/Components/PendingRespawn.cs)** — *"Added by DeathSystem, counted down and cleared by RespawnSystem."* [`DeathSystem:18`](sim/Systems/DeathSystem.cs) explicitly `continue`s on `Has<Player>`, so it never touches this. [`RespawnSystem.BeginRespawn:37`](sim/Systems/RespawnSystem.cs) adds it.
- **[`NavigationAgentSystem:168`](sim/Systems/NavigationAgentSystem.cs)** — `FP64.Atan2(nav.Velocity.x, nav.Velocity.y)` is correct (`FPVector2` is the XZ plane, so `.y` *is* Z), but it sits 100 lines from [`CommandSystem:66`](sim/Systems/CommandSystem.cs)'s `Atan2(move.x, move.z)` doing the same thing with a different-looking axis. One clause of comment would stop the next reader from "fixing" it.
- **[`SpatialHashGrid`](sim/Navigation/SpatialHashGrid.cs)'s** doc claims determinism *"regardless of hash iteration order"* while `Clear():28` enumerates `_cells.Values`. It's genuinely benign — that loop only returns lists to a pool — but the doc reads as an absolute.

### One thing that reads like a bug and isn't

[`CommandSystem.Update:59`](sim/Systems/CommandSystem.cs) and [`AttackIntentSystem:99`](sim/Systems/AttackIntentSystem.cs) remove a component from the storage the enclosing `Filter` is iterating. Klotho's `ComponentStorageFlat.Remove` is a swap-remove and `Filter` captures both the dense span and the count in its constructor, so this looks like the classic skipped-element bug.

Traced: it's safe, but only because both sites remove the **current** entity. Swap-back moves the tail element into a slot the cursor has already passed, and the stale tail slot still resolves to that same entity — so it gets visited exactly once, just later. Remove a *different* entity mid-iteration and the guarantee breaks.

Worth a comment, because the rest of the codebase takes the opposite approach: [`DeathSystem`](sim/Systems/DeathSystem.cs), [`PickupSystem`](sim/Systems/PickupSystem.cs), [`TeamPruneSystem`](sim/Systems/TeamPruneSystem.cs), [`WaveSpawnSystem`](sim/Systems/WaveSpawnSystem.cs), and [`HeroSpawnSystem`](sim/Systems/HeroSpawnSystem.cs) all snapshot into a list first, three of them with comments explaining why. Two systems relying on an unstated subtlety instead is the kind of thing that survives until someone adds a second removal.

### What's good

Worth saying, since the above is all deficits. [`AssetIds.cs`](sim/Assets/AssetIds.cs) and [`ComponentIds.cs`](sim/Components/ComponentIds.cs) are genuinely excellent — stable-id ledgers with explicit "next free" markers, the reuse hazard spelled out, and a note on *why* they're kept in numeric order rather than grouped by file. The comment culture throughout explains **why** rather than what ([`NavigationTuningAsset`](sim/Assets/NavigationTuningAsset.cs)'s settle-tuning block, [`Inventory`](sim/Components/Inventory.cs)'s fixed-buffer rationale with the byte math worked out, [`LobbyPlayerConfig`](sim/Network/LobbyPlayerConfig.cs)'s "off the deterministic path by design"). Asset-driven tuning is the dominant pattern and the five violations above are the exceptions. And the deterministic-math discipline is real — not one floating-point operation in the whole simulation.

### Top three

1. The `_lastSnappedPositions` rollback leak (silent desync).
2. The `HeroFactory` NRE (one line).
3. `DeathSystem` kill attribution (wrong gameplay outcome plus an O(n²) scan).
