## Tools
- [`SimMarkerNode`](client/Scripts/SimMarkerNode.cs) - Markers can be placed in Godot and used in sim code (spawns, shops)
- These are baked with Godot tool [`GodotFPMapLayoutExporter`](client/Scripts/Editor/GodotFPMapLayoutExporter.cs) and saved to [`Sim/Data/MapLayout.bytes`](client/Sim/Data/MapLayout.bytes)
- In the same way, we generate a deterministic navmesh to [`NavigationRegion3D.NavMeshData.bytes`](client/Sim/Data/NavigationRegion3D.NavMeshData.bytes)
- [`UnitLookup`](sim/UnitLookup.cs) provides stable identifiers for all units, and resolves them back to entities

## TODO
- **Assist gold.** `GoldRulesAsset.GoldPerAssist` (50) is authored but nothing reads it. Assists need a
damage-participation window per victim before a payout has anything to key off — `Health.LastDamagerUnitId`
only remembers the fatal hit, so the killer is the only actor a death can currently credit.

### Dead code
- **[`FlowFieldCache.Version` and `Invalidate()`](sim/Navigation/FlowFieldCache.cs)** are never called — meaning flow fields are never invalidated. Harmless while the navmesh is static (nothing writes `isBlocked` at runtime), but the API implies otherwise.

## Naming consistency
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
ProjectileSystem.IsHostile:143 has two hostility paths. It prefers the live caster's team and only
falls back to projectile.TeamId. The stamped team is the correct answer on its own — the live path
means a caster whose team changes retargets bullets already in flight, and it costs a dictionary
lookup per candidate per bullet.

Fixed-buffer accessors are publicly unchecked.
SkillsComponent.GetRank/GetSkillAssetId/GetCooldownRemainingTicks and
InventoryComponent.GetItemAssetId index fixed int buffers with no bounds check. That's documented and
gated for the skill path (CommandValidation.AcceptSkillSlot), but InventoryComponent.GetItemAssetId
has no equivalent gate described anywhere, and both are reachable from the client's UI code.


## Replace skill damage, attack damage, etc. with curves instead of simple functions
