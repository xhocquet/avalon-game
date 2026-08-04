
## Tools
- [`SimMarkerNode`](client/Scripts/SimMarkerNode.cs) - Markers can be placed in Godot and used in sim code (spawns, shops)
  - These are baked with Godot tool [`GodotFPMapLayoutExporter`](client/Scripts/Editor/GodotFPMapLayoutExporter.cs) and saved to [`Sim/Data/MapLayout.bytes`](client/Sim/Data/MapLayout.bytes)
- In the same way, we generate a deterministic navmesh to [`NavigationRegion3D.NavMeshData.bytes`](client/Sim/Data/NavigationRegion3D.NavMeshData.bytes)
- [`UnitLookup`](sim/UnitLookup.cs) provides stable identifiers for all units, and resolves them back to entities

### Dead code

- **[`Stats.Defense` and `Stats.Speed`](sim/Components/Stats.cs) (`:9-10`)** are never read anywhere in `sim/`, `client/`, `server/`, or `tests/`. They default to 100 and ride every rollback snapshot.
- **[`FlowFieldCache.Version` and `Invalidate()`](sim/Navigation/FlowFieldCache.cs)** are never called — meaning flow fields are never invalidated. Harmless while the navmesh is static, but the API implies otherwise.
- **[`Pickup.Type`](sim/Components/Pickup.cs)** is a commented-out field with a TODO.


  ## x
  Player and Hero are the same entity. HeroFactory adds both; nothing else ever adds Player. Systems pick
  whichever they feel like as the marker: DeathSystem excludes Player, RespawnSystem/ScoreSystem filter
  Player, HeroBehaviorSystem/TeamPruneSystem filter Hero, PickupSystem uses InventoryComponent,
  InventorySystem uses InventoryComponent + StatsComponent. Six spellings of one concept. The cheap fix is
  deleting Player and moving Score onto Hero; PlayerId is already on both.


  ## x
  Four different scans for "player N's hero", over three different filters:
  CommandSystem.TryGetPlayerHero (Filter<Hero>), CommandSystem.ApplyLocalHeroTarget (Filter<Player>),
  HeroSpawnSystem.HasHero (Filter<Hero>), UnitLookup.TryGetPlayerTeamId (Filter<Player, TeamComponent>).
  All now go through UnitLookup.TryGetPlayerHero.

  ## x
  TryGetUnitId copy-pasted into DamageSystem and AttackIntentSystem, with a third variant inline in
  DeathSystem — every call site wrapped in ? id : 0. Now UnitLookup.GetUnitId returns the 0.

  ## x
  Health.Current <= 0 as the death test in six systems → Health.IsAlive. This one is more than cosmetic:
  dead-but-awaiting-respawn is a real state that only heroes enter, and it's why ExperienceSystem needs
  its heal guard. Naming it makes the guard legible instead of looking like a stray null check.

  ## x
  Not fixed — your call, these are design decisions

  ## x
  Player and Hero are the same entity. HeroFactory adds both; nothing else ever adds Player. Systems
  pick whichever they feel like as the marker: DeathSystem excludes Player, RespawnSystem/ScoreSystem
  filter Player, HeroBehaviorSystem/TeamPruneSystem filter Hero, PickupSystem uses InventoryComponent,
  InventorySystem uses InventoryComponent + StatsComponent. Six spellings of one concept. The cheap fix
  is deleting Player and moving Score onto Hero; PlayerId is already on both.

  ## x
  Heroes read their reacquire range from MinionStatsAsset. TargetAcquisitionSystem.GetAcquisitionRadius
  special-cases turrets, then applies MinionStatsAsset.AttackReacquireRangeMultiplier to everything else
  — including heroes. A hero silently sourcing a number from the minion asset. Belongs on
  CombatRulesAsset, or per-hero on HeroAsset.

  ## x
  Turrets and crystals can't be auto-acquired. GetTargetPriority returns int.MaxValue for anything that
  isn't a Minion or Hero, so units walk past enemy turrets and never push a base without an explicit
  attack order. Looks deliberate for turrets; less obviously so for crystals, given crystals are the win
  condition. Worth confirming it's intent.
