
## Tools
- [`SimMarkerNode`](client/Scripts/SimMarkerNode.cs) - Markers can be placed in Godot and used in sim code (spawns, shops)
  - These are baked with Godot tool [`GodotFPMapLayoutExporter`](client/Scripts/Editor/GodotFPMapLayoutExporter.cs) and saved to [`Sim/Data/MapLayout.bytes`](client/Sim/Data/MapLayout.bytes)
- In the same way, we generate a deterministic navmesh to [`NavigationRegion3D.NavMeshData.bytes`](client/Sim/Data/NavigationRegion3D.NavMeshData.bytes)
- [`UnitLookup`](sim/UnitLookup.cs) provides stable identifiers for all units, and resolves them back to entities

### Dead code

- **[`Stats.Defense` and `Stats.Speed`](sim/Components/Stats.cs) (`:9-10`)** are never read anywhere in `sim/`, `client/`, `server/`, or `tests/`. They default to 100 and ride every rollback snapshot.
- **[`FlowFieldCache.Version` and `Invalidate()`](sim/Navigation/FlowFieldCache.cs)** are never called — meaning flow fields are never invalidated. Harmless while the navmesh is static, but the API implies otherwise.
- **[`Pickup.Type`](sim/Components/Pickup.cs)** is a commented-out field with a TODO.




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

  2. GameOverEvent reports every match as a draw — Events/GameOverEvent.cs:11-12

  int IMatchEndEvent.WinnerPlayerId => -1;
  FixedString32 IMatchEndEvent.Reason => default;

  ScoreSystem.EndMatch (Systems/ScoreSystem.cs:53-59) resolves a winner into MatchEndStateComponent and
  then raises an event that discards it. KlothoEngine.OnMatchEnded, IKlothoSessionObserver.OnMatchEnded,
  and Room's drain handler all read endEvt.WinnerPlayerId. The event also declares zero [KlothoOrder]
  fields, so it carries nothing on the wire. MatchResultReader works because it reads the component
  directly — the engine-facing path is the broken one.

  3. Combat.Target is an EntityRef living across ticks — Components/Commands/Combat.cs:16

  Directly contradicts the codebase's own rule (frame.Has<T> ignores version; store UnitId for cross-tick
  refs). It's latent today because AttackIntentSystem re-resolves it every tick before DamageSystem reads
  it — but the two filters disagree: AttackIntentSystem requires TransformComponent, DamageSystem does not.
  An attacker with Combat + AttackTargetUnitId and no transform skips re-resolution and DamageSystem acts
  on a stale ref.

  4. FlowFieldCache is unbounded — Navigation/FlowFieldCache.cs

  One int[triCount] + FPVector2[triCount] per distinct goal triangle, never evicted; Invalidate() has no
  caller in sim/. Deterministic (pure function of navmesh + goal), so not a desync — but a monotonic leak
  over a match, and TriangleFlowField.Compute is O(V²) with a linear-scan priority queue on each miss.

  5. DamageSystem.LogCooldownBoundary is dead once AttackSpeed ≠ 1 — Systems/DamageSystem.cs:59

  Compares CooldownRemainingTicks == AttackCooldownTicks - 1, but GetCooldownTicks divides the base by
  AttackSpeed. The "cooldown started" line never fires for any hero past level 1.

  6. Unchecked parallel-array indexing in MapLayoutAsset

  TryGetByTypeAndTeam indexes MarkerTeams[i]/MarkerPositions[i] off MarkerTypes.Length.
  SimulationSetup.cs:182 guards MarkerValues with an explicit length check but nothing guards the other
  three. Mismatched arrays in Assets.json throw inside the sim — and per your own AGENTS.md, an exception
  on the command path takes the server down.

  7. WaveSpawnSystem.GetFirstFreeSlot is an unbounded loop — Systems/WaveSpawnSystem.cs:81-86

  while (IsSlotOccupied(...)) slot++ with no cap, and GetSpawnPosition walks rings from 0 on every call.
  Terminates in practice; nothing enforces it.

  8. No cast range — CommandValidation.AcceptCastTarget checks only the ±1024 world envelope, and
  SkillActions.TryCast never bounds the aim point against the caster. A modified client casts across the
  map. SkillAsset has no CastRange field to check against.

  9. PurchaseItemCommand has no CommandValidation case — falls through default: return true
  (Commands/CommandValidation.cs:35) while SelectFactionCommand's asset id is registry-checked. ShopActions
  catches it, but the two-layer rule in AGENTS.md isn't applied uniformly.

  10. Target acquisition ignores distance — TargetAcquisitionSystem.cs:88 breaks ties by lowest UnitId
  after type priority. An attacker with two minions in range always shoots the older one, never the nearer
  one.

  Duplication

  ┌───────────────────────────────────────┬────────────────────────────────────────────────────────────┐
  │                 Where                 │                            What                            │
  ├───────────────────────────────────────┼────────────────────────────────────────────────────────────┤
  │                                       │ EnsureAllCapacity / EnsureHeroCapacity /                   │
  │ NavigationAgentSystem.cs:336-384      │ EnsureMinionCapacity are three copies of the generic       │
  │                                       │ EnsureCapacity sitting right beside them                   │
  ├───────────────────────────────────────┼────────────────────────────────────────────────────────────┤
  │ UnitLookup.cs:12-26,                  │ Three byte-identical Initialize/Next counter               │
  │ PickupIdGenerator.cs,                 │ implementations                                            │
  │ ProjectileIdGenerator.cs              │                                                            │
  ├───────────────────────────────────────┼────────────────────────────────────────────────────────────┤
  │ Combat.From ×3 + StatsComponent{…} ×4 │ HeroAsset/MinionStatsAsset/TurretStatsAsset each redeclare │
  │  in factories                         │  the same six fields with no shared interface, forcing an  │
  │                                       │ overload per asset type                                    │
  ├───────────────────────────────────────┼────────────────────────────────────────────────────────────┤
  │ RespawnSystem.cs:118,                 │ Ceiling ms→ticks plus the DeltaTimeMs > 0 ? : 16 fallback, │
  │ SkillActions.cs:177,                  │  written out three times                                   │
  │ ScoreSystem.cs:44                     │                                                            │
  ├───────────────────────────────────────┼────────────────────────────────────────────────────────────┤
  │ CommandValidation.cs:48-62            │ AcceptMoveTarget and AcceptCastTarget are identical apart  │
  │                                       │ from the parameter type                                    │
  ├───────────────────────────────────────┼────────────────────────────────────────────────────────────┤
  while (IsSlotOccupied(...)) slot++ with no cap, and GetSpawnPosition walks rings from 0 on every call.
  Terminates in practice; nothing enforces it.

  8. No cast range — CommandValidation.AcceptCastTarget checks only the ±1024 world envelope, and
  SkillActions.TryCast never bounds the aim point against the caster. A modified client casts across the
  map. SkillAsset has no CastRange field to check against.

  9. PurchaseItemCommand has no CommandValidation case — falls through default: return true
  (Commands/CommandValidation.cs:35) while SelectFactionCommand's asset id is registry-checked.
  ShopActions catches it, but the two-layer rule in AGENTS.md isn't applied uniformly.

  10. Target acquisition ignores distance — TargetAcquisitionSystem.cs:88 breaks ties by lowest UnitId
  after type priority. An attacker with two minions in range always shoots the older one, never the
  nearer one.

  Duplication

  ┌──────────────────────────────────────┬──────────────────────────────────────────────────────────┐
  │                Where                 │                           What                           │
  ├──────────────────────────────────────┼──────────────────────────────────────────────────────────┤
  │                                      │ EnsureAllCapacity / EnsureHeroCapacity /                 │
  │ NavigationAgentSystem.cs:336-384     │ EnsureMinionCapacity are three copies of the generic     │
  │                                      │ EnsureCapacity sitting right beside them                 │
  ├──────────────────────────────────────┼──────────────────────────────────────────────────────────┤
  │ UnitLookup.cs:12-26,                 │ Three byte-identical Initialize/Next counter             │
  │ PickupIdGenerator.cs,                │ implementations                                          │
  │ ProjectileIdGenerator.cs             │                                                          │
  ├──────────────────────────────────────┼──────────────────────────────────────────────────────────┤
  │ Combat.From ×3 + StatsComponent{…}   │ HeroAsset/MinionStatsAsset/TurretStatsAsset each         │
  │ ×4 in factories                      │ redeclare the same six fields with no shared interface,  │
  │                                      │ forcing an overload per asset type                       │
  ├──────────────────────────────────────┼──────────────────────────────────────────────────────────┤
  │ RespawnSystem.cs:118,                │ Ceiling ms→ticks plus the DeltaTimeMs > 0 ? : 16         │
  │ SkillActions.cs:177,                 │ fallback, written out three times                        │
  │ ScoreSystem.cs:44                    │                                                          │
  ├──────────────────────────────────────┼──────────────────────────────────────────────────────────┤
  │ CommandValidation.cs:48-62           │ AcceptMoveTarget and AcceptCastTarget are identical      │
  │                                      │ apart from the parameter type                            │
  ├──────────────────────────────────────┼──────────────────────────────────────────────────────────┤
  │ UnitLookup.TryGetEntityByUnitId vs   │ Two lookup mechanisms for the same question;             │
  │ UnitLookup.Index                     │ RespawnSystem.AwardKillExperience:65 uses the O(n) scan  │
  │                                      │ for the same job DeathSystem:104 does with the index     │
  ├──────────────────────────────────────┼──────────────────────────────────────────────────────────┤
  │                                      │ Identical 4-case switch ((SkillSlot)ctx.Slot) dispatch   │
  │ 5× *Skills.cs                        │ per hero — a slot→method table on the base would remove  │
  │                                      │ it                                                       │
  └──────────────────────────────────────┴──────────────────────────────────────────────────────────┘

  Naming consistency

  - Namespace split. Everything under Systems/ declares namespace Meesles.Avalon; Components/, Assets/,
  Commands/, Heroes/, Navigation/, Factories/ all use Meesles.Avalon.Sim.*. Result: every system file
  opens with using Meesles.Avalon.Sim;. Nothing in AGENTS.md explains it.
  - Component suffixes are 50/50. Health, Hero, Minion, Combat, Crystal, Turret, Pickup, Oasis bare vs
  TeamComponent, StatsComponent, SkillsComponent, FactionComponent, InventoryComponent,
  ExperienceComponent, UnitIdComponent suffixed. ComponentIds then uses a third naming (Unit, Faction,
  Stats, Skills, Experience), so ComponentIds.Unit names UnitIdComponent.
  - HeroAsset vs MinionStatsAsset/TurretStatsAsset/CrystalStatsAsset — same role, one drops Stats.
  - TurretStatsAsset declares fields in the order 3,1,2,0,4,5 relative to its KlothoOrder values. Works,
  reads as an accident.
  - WaveSpawnSystem.cs:36-37 binds frame.Get<T> to ref readonly where every other read site uses
  GetReadOnly.
  - Logging bypasses SimLog. AGENTS.md:87 says gameplay logging goes through SimLog so replayed ticks
  stay quiet, but CommandSystem.cs:105, AttackIntentSystem.cs:90, and DamageSystem.cs:67 call
  no explicit EventMode. The projectile pair is documented as deliberately Regular; AttackHitEvent isn't
  mentioned anywhere.
  - Stray Events/GameOverEvent.cs.uid checked in.

  Design gaps

  StatsComponent is the weakest abstraction in the sim. Add truncates via .ToInt() for
  Strength/Defense/MaxHealth, so no fractional or percentage modifier is expressible — a +0.5 Strength
  item is a no-op. There's no clamping (MaxHealth can reach ≤ 0; negative Defense makes Mitigate return
  raw damage), and no default: case, so a newly added StatType silently does nothing. GoldPerTick also
  sits in what is otherwise a combat block.

  No HealthApplication to match DamageApplication. DamageApplication is a genuinely good single choke
  point for damage. Healing has no equivalent: ExperienceSystem.ApplyLevelGains:47 adds to Current with
  no clamp, and RespawnSystem:79 reaches into StatsComponent.MaxHealth itself. Any future heal/regen
  effect will re-derive the max-clamp rule a third time.

  Match end can't express a team win. ScoreSystem.TryEvaluateCrystalWin:66 returns false unless ≥2 teams
  are active, and TryGetPlayerIdForTeam collapses a winning team to its lowest PlayerId. MatchEndReason
  is then inferred from winner == -1 in MatchResultReader:47 rather than recorded at the point the
  match ended — a crystal win with an unresolvable player reads as Timeout.

  The IHeroBehavior layer is currently dead weight. One enum value, an empty DefaultHeroBehavior, and
  HeroBehaviorSystem allocating a snapshot list every tick to call it. IHeroSkillSet is the layer doing
  real work; behaviors could fold into it or be deleted until something needs them.

  ProjectileSystem.IsHostile:143 has two hostility paths. It prefers the live caster's team and only
  falls back to projectile.TeamId. The stamped team is the correct answer on its own — the live path
  means a caster whose team changes retargets bullets already in flight, and it costs a dictionary
  lookup per candidate per bullet.

  PickupSystem has no broad phase (its own TODO at line 9) — pickups × collectors every tick, while
  every other proximity query in the sim uses SpatialHashGrid.

  Fixed-buffer accessors are publicly unchecked.
  SkillsComponent.GetRank/GetSkillAssetId/GetCooldownRemainingTicks and
  InventoryComponent.GetItemAssetId index fixed int buffers with no bounds check. That's documented and
  gated for the skill path (CommandValidation.AcceptSkillSlot), but InventoryComponent.GetItemAssetId
  has no equivalent gate described anywhere, and both are reachable from the client's UI code.

  ---
  Want me to start fixing? My suggested order: (2) the match-end winner, (6) the marker-array guards,
  (1) converting the three in-loop removals to deferred lists, then the duplication table — those are
  mechanical and low-risk.
