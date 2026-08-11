using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using Meesles.Avalon.Sim.Factories;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using Xunit;

namespace Meesles.Avalon.Sim.Tests;

// Crystal Giant's Tertiary: the first skill with a body, and the whole projectile lifecycle with it.
//
// Targets are built by hand rather than spawned through MinionFactory so they hold still: a real
// minion carries a NavAgentComponent and NavigationAgentSystem would snap and steer it mid-flight,
// which is not what any of these assertions are about. The hand-built dummies carry exactly what the
// hit path reads - id, team, health, transform, and the unit-kind marker.
public class CrystalBulletsTests {
  private const int CasterPlayerId = 1;
  private const int CasterTeamId = 1;
  private const int EnemyTeamId = 2;
  private const int Tertiary = (int)SkillSlot.Tertiary;

  [Fact]
  public void Cast_PutsThreeBarsAbreastAllTravellingTheSameWay() {
    var harness = CreateCrystalGiantHarness();

    var origin = LearnAndCastAlongX(harness);

    var shots = Projectiles(harness);
    shots.Should().HaveCount(3);
    shots.Select(s => s.Component.Index).Should().Equal(0, 1, 2);

    // Parallel, not fanned: one shared direction, offsets purely lateral.
    var skill = CrystalBulletsAsset(harness);
    shots.Select(s => s.Component.Direction).Should().AllBeEquivalentTo(FPVector3.Right);

    // Aimed along +X, so the spread runs along Z at the authored spacing, centred on the caster.
    var spacing = skill.ProjectileSpacing;
    shots.Select(s => s.Position.z).Should()
      .Equal(origin.z + spacing, origin.z, origin.z - spacing);
    shots.Select(s => s.Position.x).Distinct().Should().ContainSingle("all three advance together");
  }

  // The whole skill is authored in one asset row. Nothing about a bullet may be decided in
  // code, so every field on a spawned projectile is checked back against the asset it came from -
  // a hardcoded speed or damage fails here rather than quietly shipping.
  [Fact]
  public void EveryProjectileNumber_TracesBackToTheAssetRow() {
    var harness = CreateCrystalGiantHarness();
    var skill = CrystalBulletsAsset(harness);

    LearnAndCastAlongX(harness);

    var shots = Projectiles(harness);
    shots.Should().HaveCount(skill.ProjectileCount);
    shots.Should().OnlyContain(s => s.Component.Speed == skill.ProjectileSpeed);
    shots.Should().OnlyContain(s => s.Component.Radius == skill.ProjectileRadius);
    shots.Should().OnlyContain(s => s.Component.Damage == skill.DamageAtRank(1));

    // Range is measured from the muzzle, so a bullet one tick old has burned exactly one step of it.
    var remaining = skill.ProjectileRange - StepPerTick(skill);
    shots.Should().OnlyContain(s => s.Component.RemainingDistance == remaining);

    var lateral = shots[0].Position.z - shots[1].Position.z;
    lateral.Should().Be(skill.ProjectileSpacing);
  }

  // The reference telegraph leaves a gap in front of the caster rather than starting at its feet
  // (demo_spell_7 centres its bars at z = -6 with half-length 5, so they span -1 to -11).
  [Fact]
  public void TheVolleyStartsAtTheAuthoredOffsetAheadOfTheCaster() {
    var harness = CreateCrystalGiantHarness();
    var skill = CrystalBulletsAsset(harness);
    skill.ProjectileSpawnOffset.Should().BeGreaterThan(FP64.Zero, "otherwise this proves nothing");

    var origin = LearnAndCastAlongX(harness);

    // Aimed along +X, one tick after the cast.
    var expectedX = origin.x + skill.ProjectileSpawnOffset + StepPerTick(skill);
    Projectiles(harness).Should().OnlyContain(s => s.Position.x == expectedX);
  }

  [Fact]
  public void Cast_RaisesOneSpawnEventPerBarWithDistinctIds() {
    var harness = CreateCrystalGiantHarness();
    var collector = CollectEvents(harness);

    LearnAndCastAlongX(harness);

    var spawned = collector.Collected.OfType<SkillProjectileSpawnedEvent>().ToList();
    spawned.Should().HaveCount(3);
    spawned.Select(e => e.ProjectileId).Distinct().Should().HaveCount(3);
    spawned.Select(e => e.Index).Should().Equal(0, 1, 2);

    var casterUnitId = UnitId(harness, harness.FindHero(CasterPlayerId));
    spawned.Should().OnlyContain(e => e.SourceUnitId == casterUnitId);
    spawned.Should().OnlyContain(e => e.SkillAssetId == AssetIds.SkillCrystalGiantTertiary);
    spawned.Should().OnlyContain(e => e.Slot == Tertiary);

    collector.Collected.OfType<SkillCastEvent>().Should().ContainSingle();
  }

  [Fact]
  public void AnEnemyOnOneBarsPath_TakesTheHitAndStopsOnlyThatBar() {
    var harness = CreateCrystalGiantHarness();
    var skill = CrystalBulletsAsset(harness);

    // Dead centre of the volley, five units out - the middle bar's line exactly. Placed after the
    // cast so it sits on the line the volley actually took, not on a pre-navmesh-snap guess.
    var origin = LearnAndCastAlongX(harness);
    var enemy = SpawnDummy(harness, origin + FPVector3.Right * FP64.FromInt(5), EnemyTeamId, isMinion: true);
    var healthBefore = harness.Frame.GetReadOnly<Health>(enemy).Current;

    AdvanceUntilTheVolleyClears(harness, skill);

    harness.Frame.GetReadOnly<Health>(enemy).Current
      .Should().Be(healthBefore - skill.DamageAtRank(1));
  }

  [Fact]
  public void AHitConsumesOnlyTheBarThatLanded() {
    var harness = CreateCrystalGiantHarness();
    var origin = LearnAndCastAlongX(harness);
    SpawnDummy(harness, origin + FPVector3.Right * FP64.FromInt(5), EnemyTeamId, isMinion: true);
    var collector = CollectEvents(harness);

    // Far enough for the middle bar to reach the target, nowhere near the other two expiring.
    for (var i = 0; i < 30; i++)
      harness.Tick();

    harness.Count<Projectile>().Should().Be(2);
    var despawned = collector.Collected.OfType<SkillProjectileDespawnedEvent>().ToList();
    despawned.Should().ContainSingle();
    despawned[0].Reason.Should().Be((int)SkillProjectileEnd.Hit);
    despawned[0].HitUnitId.Should().NotBe(0);
  }

  [Fact]
  public void AFriendlyOnThePath_IsNeitherHitNorAnObstacle() {
    var harness = CreateCrystalGiantHarness();
    var skill = CrystalBulletsAsset(harness);

    var origin = LearnAndCastAlongX(harness);
    var friendly = SpawnDummy(harness, origin + FPVector3.Right * FP64.FromInt(5), CasterTeamId, isMinion: true);
    var healthBefore = harness.Frame.GetReadOnly<Health>(friendly).Current;

    AdvanceUntilTheVolleyClears(harness, skill);

    harness.Frame.GetReadOnly<Health>(friendly).Current.Should().Be(healthBefore);
  }

  [Fact]
  public void AnEnemyTurretOnThePath_IsNeitherHitNorAnObstacle() {
    var harness = CreateCrystalGiantHarness();
    var skill = CrystalBulletsAsset(harness);

    // Structures are excluded from skill hits by default - see CombatTargeting.IsSkillHittable.
    var origin = LearnAndCastAlongX(harness);
    var turret = SpawnDummy(harness, origin + FPVector3.Right * FP64.FromInt(5), EnemyTeamId, isMinion: false);
    var healthBefore = harness.Frame.GetReadOnly<Health>(turret).Current;

    AdvanceUntilTheVolleyClears(harness, skill);

    harness.Frame.GetReadOnly<Health>(turret).Current.Should().Be(healthBefore);
  }

  [Fact]
  public void AVolleyThatHitsNothing_ExpiresAtItsAuthoredRange() {
    var harness = CreateCrystalGiantHarness();
    var skill = CrystalBulletsAsset(harness);

    LearnAndCastAlongX(harness);
    var collector = CollectEvents(harness);
    AdvanceUntilTheVolleyClears(harness, skill);

    harness.Count<Projectile>().Should().Be(0);
    var despawned = collector.Collected.OfType<SkillProjectileDespawnedEvent>().ToList();
    despawned.Should().HaveCount(3);
    despawned.Should().OnlyContain(e => e.Reason == (int)SkillProjectileEnd.Expired);
    despawned.Should().OnlyContain(e => e.HitUnitId == 0);
  }

  [Fact]
  public void EveryBornProjectileIdIsAlsoBuried() {
    var harness = CreateCrystalGiantHarness();
    var skill = CrystalBulletsAsset(harness);

    var collector = CollectEvents(harness);
    var origin = LearnAndCastAlongX(harness);
    SpawnDummy(harness, origin + FPVector3.Right * FP64.FromInt(5), EnemyTeamId, isMinion: true);
    AdvanceUntilTheVolleyClears(harness, skill);

    var born = collector.Collected.OfType<SkillProjectileSpawnedEvent>().Select(e => e.ProjectileId);
    var buried = collector.Collected.OfType<SkillProjectileDespawnedEvent>().Select(e => e.ProjectileId);
    buried.Should().BeEquivalentTo(born);
  }

  [Fact]
  public void ALethalHit_CreditsTheCasterSoTheKillPaysXp() {
    var harness = CreateCrystalGiantHarness();
    var skill = CrystalBulletsAsset(harness);
    var xpBefore = harness.Frame
      .GetReadOnly<ExperienceComponent>(harness.FindHero(CasterPlayerId)).Experience;
    var expectedXp = harness.AssetRegistry.Get<XpRulesAsset>().XpPerMinionKill;

    var origin = LearnAndCastAlongX(harness);
    var enemy = SpawnDummy(harness, origin + FPVector3.Right * FP64.FromInt(5), EnemyTeamId, isMinion: true);
    harness.Frame.Get<Health>(enemy).Current = 1;

    AdvanceUntilTheVolleyClears(harness, skill);

    // Kill credit rides Health.LastDamagerUnitId, which ApplyDamage sets; without it DeathSystem
    // resolves no killer and the kill pays nobody.
    harness.Frame.GetReadOnly<ExperienceComponent>(harness.FindHero(CasterPlayerId))
      .Experience.Should().Be(xpBefore + expectedXp);
  }

  [Fact]
  public void CastAimedAtTheCastersOwnFeet_FiresAlongItsFacingRatherThanNowhere() {
    var harness = CreateCrystalGiantHarness();
    var origin = HeroPosition(harness);

    harness.Tick(SimHarness.UpgradeSkillCommand(CasterPlayerId, 0, Tertiary));
    harness.Tick(SimHarness.CastSkillCommand(CasterPlayerId, 1, Tertiary, origin.x, origin.z));

    var shots = Projectiles(harness);
    shots.Should().HaveCount(3);
    shots.Should().OnlyContain(s => s.Component.Direction.sqrMagnitude > FP64.Zero);
  }

  // --- setup helpers ---

  // The harness defaults every player to Hairy Wizards, whose skill set is empty. Crystal Bullets
  // needs the Crystal Giant row, so go through the real faction-select path instead.
  private static SimHarness CreateCrystalGiantHarness() {
    var harness = SimHarness.CreateInitialized(spawnHeroesNow: false);
    harness.Tick(
      SimHarness.SelectFactionCommand(1, 0, AssetIds.FactionCrystalWarriors),
      SimHarness.SelectFactionCommand(2, 0, AssetIds.FactionCrystalWarriors));

    DisableAutoAttacks(harness);
    return harness;
  }

  // A test dummy needs the same id/team/health a real unit has, which also makes it a legal
  // auto-attack target for the caster and for whatever base turrets are in range. Stripping Combat
  // off the board leaves the projectile as the only thing on the map that can deal damage, so a
  // health delta means exactly one thing.
  private static void DisableAutoAttacks(SimHarness harness) {
    var frame = harness.Frame;
    var attackers = new List<EntityRef>();

    var filter = frame.Filter<Combat>();
    while (filter.Next(out var entity))
      attackers.Add(entity);

    foreach (var entity in attackers)
      frame.Remove<Combat>(entity);
  }

  // Returns the caster position the volley actually fired from. Read after the upgrade tick and
  // before the cast tick, because commands run ahead of the Update phase: this is exactly what
  // SkillActions sees. Reading it any earlier catches the hero before NavigationAgentSystem snaps it
  // onto the mesh, and the aim would be off by that snap.
  private static FPVector3 LearnAndCastAlongX(SimHarness harness) {
    harness.Tick(SimHarness.UpgradeSkillCommand(CasterPlayerId, 0, Tertiary));

    var origin = HeroPosition(harness);
    harness.Tick(SimHarness.CastSkillCommand(CasterPlayerId, 1, Tertiary,
      origin.x + FP64.FromInt(20), origin.z));

    return origin;
  }

  // Distance one projectile covers in a tick, in the same order of operations ProjectileSystem uses
  // so the fixed-point result is bit-identical.
  private static FP64 StepPerTick(SkillAsset skill) {
    return skill.ProjectileSpeed * (FP64.FromInt(SimHarness.DefaultDeltaTimeMs) / FP64.FromInt(1000));
  }

  // One tick per step of range, plus slack, so nothing is still in the air afterwards.
  private static void AdvanceUntilTheVolleyClears(SimHarness harness, SkillAsset skill) {
    var dt = FP64.FromInt(SimHarness.DefaultDeltaTimeMs) / FP64.FromInt(1000);
    var ticks = (skill.ProjectileRange / (skill.ProjectileSpeed * dt)).ToInt() + 4;
    for (var i = 0; i < ticks; i++)
      harness.Tick();
  }

  private static EntityRef SpawnDummy(SimHarness harness, FPVector3 position, int teamId, bool isMinion) {
    var frame = harness.Frame;
    var entity = frame.CreateEntity();

    frame.Add(entity, TransformFactory.At(position));
    frame.Add(entity, new UnitIdComponent {
      UnitId = UnitLookup.NextUnitId(ref frame),
      UnitTypeId = isMinion ? SimulationSetup.MinionUnitTypeId : SimulationSetup.TurretUnitTypeId
    });
    frame.Add(entity, new TeamComponent(teamId));
    frame.Add(entity, new Health(500));

    if (isMinion)
      frame.Add(entity, new Minion { WaveId = 0 });
    else
      frame.Add(entity, new Turret { TurretId = 99 });

    return entity;
  }

  // Frame is a single long-lived instance, so the raiser stays attached across Tick() calls and the
  // collector accumulates a whole volley's worth of events.
  private static EventCollector CollectEvents(SimHarness harness) {
    var collector = new EventCollector();
    collector.BeginTick(harness.Frame.Tick);
    harness.Frame.EventRaiser = collector;
    return collector;
  }

  // --- readers ---

  private static SkillAsset CrystalBulletsAsset(SimHarness harness) {
    return harness.AssetRegistry.Get<SkillAsset>(AssetIds.SkillCrystalGiantTertiary);
  }

  private static FPVector3 HeroPosition(SimHarness harness) {
    return harness.Frame.GetReadOnly<TransformComponent>(harness.FindHero(CasterPlayerId)).Position;
  }

  private static int UnitId(SimHarness harness, EntityRef entity) {
    return harness.Frame.GetReadOnly<UnitIdComponent>(entity).UnitId;
  }

  private static List<(Projectile Component, FPVector3 Position)> Projectiles(SimHarness harness) {
    var frame = harness.Frame;
    var found = new List<(Projectile, FPVector3)>();

    var filter = frame.Filter<Projectile, TransformComponent>();
    while (filter.Next(out var entity))
      found.Add((frame.GetReadOnly<Projectile>(entity), frame.GetReadOnly<TransformComponent>(entity).Position));

    return found.OrderBy(p => p.Item1.Index).ToList();
  }
}
