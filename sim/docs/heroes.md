# Heroes

- [`HeroAsset`](../Assets/HeroAsset.cs) holds every hero number. `FactionAsset.HeroAssetId` selects the row; the spawned entity keeps it in `Hero.HeroAssetId`.
- Distances are metres — published stat blocks are 100x, so a 350 move speed is authored 3.5. Regen is per 5s.
- The row is a spawn seed. `StatsComponent.From` copies it once, `Stats.Add(StatType, delta)` owns it after. Never read a live value back off it.
- `Base*` + `*PerLevel` pairs grow with level (see XP & Leveling). Flat fields don't.

| Field | Lands on | Notes |
| --- | --- | --- |
| `BaseHealth` | `Stats.MaxHealth` | `Health.Current` is transient, not a stat |
| `BaseAttackDamage` | `Stats.AttackDamage` | |
| `BaseAttackSpeed` | `Stats.AttacksPerSecond` | attacks/sec, scaled by `Stats.BonusAttackSpeed` |
| `BaseArmor`, `BaseMagicResist` | `Stats.Armor`, `Stats.MagicResist` | `DamageApplication.Mitigate` picks by `DamageType` |
| `MoveSpeed` | `Stats.MoveSpeed` | pushed onto the nav agent each tick |
| `AttackRange` | `Stats.AttackRange` | edge-to-edge |
| `AcquisitionRange` | `Stats.AcquisitionRange` | absolute, not a multiple of attack range |
| `CritChance`, `CritDamage` | `Stats.CritChance`, `Stats.CritDamage` | |
| `PathingRadius` | nav agent | |
| `GameplayRadius` | `Stats.GameplayRadius` | what a hit tests against |
| `SelectionRadius` | — | view only |
| `AttackWindup`, `AttackSpeedRatio`, mana, regens | — | unread |

- [`CombatRange.ReachSq`](../CombatRange.cs) is the only conversion from edge-to-edge range to centre distance, adding both `GameplayRadius`. Centre-to-centre puts turrets out of melee reach — their navmesh hole is ~1.6m wide against a 1.25m reach.
- No attack period is stored. [`CombatTiming.CooldownTicks`](../CombatTiming.cs) derives it per hit, so rate bonuses stay additive and rounding never compounds.
- Crit is opt-in per damage source — `DamageSystem` passes `canCrit: true` for auto-attacks, skills don't. It multiplies pre-mitigation.
- `BehaviorId` selects an [`IHeroBehavior`](../Heroes/IHeroBehavior.cs): `OnSpawn` from `HeroFactory`, `OnTick` from [`HeroBehaviorSystem`](../Systems/HeroBehaviorSystem.cs). Skills dispatch separately (see Skills & Upgrades).
- Behaviors are stateless singletons — a field on one survives rollback and desyncs. Put state in a component, added in `OnSpawn`.
- Components are `[StructLayout(Sequential)]` structs: no subclassing, and a new hero must not change the shared layout.
- New hero: ids in `AssetIds`, `heroes/<hero>.json` with one `FactionAsset`, one `HeroAsset` and four `SkillAsset` rows, regenerate `Assets.bytes`. Code only for behavior no `BehaviorId` covers.
