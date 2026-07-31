
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

### Defensive code

**[`MapLayoutAsset.TryGetByTypeAndTeam`](sim/Assets/MapLayoutAsset.cs) (`:19-22`) trusts the parallel-array invariant it can't see.** It null-checks `MarkerTypes`, then indexes `MarkerTeams[i]` and `MarkerPositions[i]` with the same `i`. A short or null companion array from a bad `Assets.json` throws deep inside world init. [`SimulationSetup.SpawnPickups:156`](sim/SimulationSetup.cs) does the right thing for the fourth array (`MarkerValues != null && i < MarkerValues.Length`) — the asset should enforce that for all four, once, rather than each caller remembering.


### Style consistency

Most of this is cosmetic, but it's the kind that accumulates:

- **Namespace vs. folder.** Every file in [`sim/Systems/`](sim/Systems) declares `namespace Meesles.Avalon`, while everything else in `sim/` uses `Meesles.Avalon.Sim`, `.Sim.Components`, `.Sim.Navigation`, etc. Systems are the only directory whose namespace doesn't track its path.
- **[`Enums.cs`](sim/Enums.cs)** bundles three unrelated enums into a catch-all while every other type gets its own file.
- **[`NavigationAgentSystem` field order](sim/Systems/NavigationAgentSystem.cs) (`:16-40`)** looks alphabetized by tooling — `_heroCount` sits between the grids and the hero arrays, `_minionCount` between `_lastSnappedPositions` and `_minionEntities`, splitting comment blocks from what they document.
- **[`TargetAcquisitionSystem:122`](sim/Systems/TargetAcquisitionSystem.cs) reads `MinionStatsAsset.AttackReacquireRangeMultiplier` for every attacker**, heroes and turrets included. Heroes now take their combat profile from [`HeroAsset`](sim/Assets/HeroAsset.cs), so this is the last field where the minion row still speaks for everyone; it's a rule, not a stat, and belongs on `CombatRulesAsset`.

### Organization & duplication

**Four capacity helpers where one already exists.** [`NavigationAgentSystem`](sim/Systems/NavigationAgentSystem.cs) has a correct generic `EnsureCapacity(ref EntityRef[], int)` at `:351` — and then `EnsureHeroCapacity` (`:359`), `EnsureMinionCapacity` (`:367`), and `EnsureAllCapacity` (`:324`) reimplement the identical doubling loop. Only `EnsureAllCapacity` justifies itself (it resizes a parallel array); the other two are pure copy-paste.

### Dead code

- **[`Stats.Defense` and `Stats.Speed`](sim/Components/Stats.cs) (`:9-10`)** are never read anywhere in `sim/`, `client/`, `server/`, or `tests/`. They default to 100 and ride every rollback snapshot.
- **[`FlowFieldCache.Version` and `Invalidate()`](sim/Navigation/FlowFieldCache.cs)** are never called — meaning flow fields are never invalidated. Harmless while the navmesh is static, but the API implies otherwise.
- **[`Pickup.Type`](sim/Components/Pickup.cs)** is a commented-out field with a TODO.

### One thing that reads like a bug and isn't

[`CommandSystem.Update:59`](sim/Systems/CommandSystem.cs) and [`AttackIntentSystem:99`](sim/Systems/AttackIntentSystem.cs) remove a component from the storage the enclosing `Filter` is iterating. Klotho's `ComponentStorageFlat.Remove` is a swap-remove and `Filter` captures both the dense span and the count in its constructor, so this looks like the classic skipped-element bug.

Traced: it's safe, but only because both sites remove the **current** entity. Swap-back moves the tail element into a slot the cursor has already passed, and the stale tail slot still resolves to that same entity — so it gets visited exactly once, just later. Remove a *different* entity mid-iteration and the guarantee breaks.

Worth a comment, because the rest of the codebase takes the opposite approach: [`DeathSystem`](sim/Systems/DeathSystem.cs), [`PickupSystem`](sim/Systems/PickupSystem.cs), [`TeamPruneSystem`](sim/Systems/TeamPruneSystem.cs), [`WaveSpawnSystem`](sim/Systems/WaveSpawnSystem.cs), and [`HeroSpawnSystem`](sim/Systems/HeroSpawnSystem.cs) all snapshot into a list first, three of them with comments explaining why. Two systems relying on an unstated subtlety instead is the kind of thing that survives until someone adds a second removal.
