## Tools
- [`SimMarkerNode`](client/Scripts/SimMarkerNode.cs) - Markers can be placed in Godot and used in sim code (spawns, shops)
- These are baked with Godot tool [`GodotFPMapLayoutExporter`](client/Scripts/Editor/GodotFPMapLayoutExporter.cs) and saved to [`Sim/Data/MapLayout.bytes`](client/Sim/Data/MapLayout.bytes)
- In the same way, we generate a deterministic navmesh to [`NavigationRegion3D.NavMeshData.bytes`](client/Sim/Data/NavigationRegion3D.NavMeshData.bytes)
- [`UnitLookup`](sim/UnitLookup.cs) provides stable identifiers for all units, and resolves them back to entities

### Dead code

- **[`Stats.Defense` and `Stats.Speed`](sim/Components/Stats.cs) (`:9-10`)** are never read anywhere in `sim/`, `client/`, `server/`, or `tests/`. They default to 100 and ride every rollback snapshot.
- **[`FlowFieldCache.Version` and `Invalidate()`](sim/Navigation/FlowFieldCache.cs)** are never called — meaning flow fields are never invalidated. Harmless while the navmesh is static (nothing writes `isBlocked` at runtime), but the API implies otherwise.

Correctness & determinism

1. Mutating the filtered storage mid-iteration (architectural hazard)

Filter snapshots Count and holds a span over the dense array (vendor/Klotho/.../Filter.cs:14-20), while
ComponentStorageFlat.Remove compacts by swap-back. Removing the current entity's iterated component
survives only because the stale dense tail past Count still resolves through Has(). Removing it from a
different entity double-visits — concretely, dense [A,B,C,D]: visit A, remove B → D swaps to slot 1, D is
visited at slot 1, then again at its stale slot 3.

Sites doing this: Systems/CommandSystem.cs:70, Systems/AttackIntentSystem.cs:23,30-31,
Systems/OasisSpawnSystem.cs:79,93. Meanwhile DeathSystem, ProjectileSystem, PickupSystem, and
TeamPruneSystem all defer removals into a list for exactly this reason. The invariant that makes the
first group safe is undocumented and one refactor ("also clear the target's intent") away from breaking.
Pick one convention.


Naming consistency

- Namespace split. Everything under Systems/ declares namespace Meesles.Avalon; Components/, Assets/,
Commands/, Heroes/, Navigation/, Factories/ all use Meesles.Avalon.Sim.*. Result: every system file
opens with using Meesles.Avalon.Sim;. Nothing in AGENTS.md explains it.
- Component suffixes are 50/50. Health, Hero, Minion, Combat, Crystal, Turret, Pickup, Oasis bare vs
TeamComponent, StatsComponent, SkillsComponent, FactionComponent, InventoryComponent,
ExperienceComponent, UnitIdComponent suffixed. ComponentIds then uses a third naming (Unit, Faction,
Stats, Skills, Experience), so ComponentIds.Unit names UnitIdComponent.
- HeroAsset vs MinionStatsAsset/TurretStatsAsset/CrystalStatsAsset — same role, one drops Stats.
- Logging bypasses SimLog. AGENTS.md:87 says gameplay logging goes through SimLog so replayed ticks
stay quiet, but CommandSystem.cs:105, AttackIntentSystem.cs:90, and DamageSystem.cs:67 call
no explicit EventMode. The projectile pair is documented as deliberately Regular; AttackHitEvent isn't
mentioned anywhere.

## Design gaps

StatsComponent is the weakest abstraction in the sim. Add truncates via .ToInt() for
Strength/Defense/MaxHealth, so no fractional or percentage modifier is expressible — a +0.5 Strength
item is a no-op. There's no clamping (MaxHealth can reach ≤ 0; negative Defense makes Mitigate return
raw damage), and no default: case, so a newly added StatType silently does nothing. GoldPerTick also
sits in what is otherwise a combat block.


ProjectileSystem.IsHostile:143 has two hostility paths. It prefers the live caster's team and only
falls back to projectile.TeamId. The stamped team is the correct answer on its own — the live path
means a caster whose team changes retargets bullets already in flight, and it costs a dictionary
lookup per candidate per bullet.

Fixed-buffer accessors are publicly unchecked.
SkillsComponent.GetRank/GetSkillAssetId/GetCooldownRemainingTicks and
InventoryComponent.GetItemAssetId index fixed int buffers with no bounds check. That's documented and
gated for the skill path (CommandValidation.AcceptSkillSlot), but InventoryComponent.GetItemAssetId
has no equivalent gate described anywhere, and both are reachable from the client's UI code.
