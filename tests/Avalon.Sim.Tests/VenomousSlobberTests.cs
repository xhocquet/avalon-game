using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using Meesles.Avalon.Sim.Factories;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using Xunit;

namespace Meesles.Avalon.Sim.Tests;

// Shroom's Primary: the cone lifecycle. Nothing travels - the wedge resolves on the cast tick - so
// every assertion here reads health straight after the cast command goes in.
//
// Targets are built by hand for the same reason CrystalBulletsTests builds its own: a real minion
// carries a nav agent that would steer it out of the wedge mid-test.
public class VenomousSlobberTests {
  private const int CasterPlayerId = 1;
  private const int CasterTeamId = 1;
  private const int EnemyTeamId = 2;
  private const int Primary = (int)SkillSlot.Primary;

  [Fact]
  public void AnEnemyInTheWedge_TakesTheRowsDamage() {
    var harness = CreateShroomHarness();
    var skill = SlobberAsset(harness);
    var origin = LearnAndPrepare(harness);

    var enemy = SpawnDummy(harness, Ahead(origin, 3, 0), EnemyTeamId, isMinion: true);
    var healthBefore = HealthOf(harness, enemy);

    CastAlongX(harness, origin);

    HealthOf(harness, enemy).Should().Be(healthBefore - skill.DamageAtRank(1));
  }

  // The whole skill is authored in its row. The rank curve the design asks for - 50 a rank - is the
  // row's business too, so it is checked here rather than assumed by the cast path.
  [Fact]
  public void DamageIsFiftyPerRank_AndTheWedgeComesOffTheRow() {
    var skill = SlobberAsset(CreateShroomHarness());

    for (var rank = 1; rank <= skill.MaxRank; rank++)
      skill.DamageAtRank(rank).Should().Be(FP64.FromInt(rank * 50));

    skill.HasCone.Should().BeTrue();
    skill.MaxCastRange.Should().Be(skill.ConeRange, "the aim clamp is what the telegraph draws");
  }

  [Fact]
  public void EveryEnemyInTheWedge_IsHitByTheOneCast() {
    var harness = CreateShroomHarness();
    var skill = SlobberAsset(harness);
    var origin = LearnAndPrepare(harness);

    // Spread across the wedge: dead centre, off to one side, and out near its reach.
    var enemies = new[] {
      SpawnDummy(harness, Ahead(origin, 2, 0), EnemyTeamId, isMinion: true),
      SpawnDummy(harness, Ahead(origin, 3, 1), EnemyTeamId, isMinion: true),
      SpawnDummy(harness, Ahead(origin, 5, -1), EnemyTeamId, isMinion: true)
    };
    var before = enemies.Select(e => HealthOf(harness, e)).ToList();

    CastAlongX(harness, origin);

    for (var i = 0; i < enemies.Length; i++)
      HealthOf(harness, enemies[i]).Should().Be(before[i] - skill.DamageAtRank(1));
  }

  [Fact]
  public void AnEnemyPastTheWedgesAngle_IsMissed() {
    var harness = CreateShroomHarness();
    var origin = LearnAndPrepare(harness);

    // 45 degrees off the aim line, outside the authored 60-degree opening.
    var enemy = SpawnDummy(harness, Ahead(origin, 3, 3), EnemyTeamId, isMinion: true);
    var healthBefore = HealthOf(harness, enemy);

    CastAlongX(harness, origin);

    HealthOf(harness, enemy).Should().Be(healthBefore);
  }

  [Fact]
  public void AnEnemyBehindTheCaster_IsMissed() {
    var harness = CreateShroomHarness();
    var origin = LearnAndPrepare(harness);

    var enemy = SpawnDummy(harness, Ahead(origin, -3, 0), EnemyTeamId, isMinion: true);
    var healthBefore = HealthOf(harness, enemy);

    CastAlongX(harness, origin);

    HealthOf(harness, enemy).Should().Be(healthBefore);
  }

  // On the aim line but past the reach. The aim point is clamped to the cast band rather than
  // rejected, so this also proves the clamp cannot drag the wedge out to meet a distant target.
  [Fact]
  public void AnEnemyPastTheConesReach_IsMissed() {
    var harness = CreateShroomHarness();
    var skill = SlobberAsset(harness);
    var origin = LearnAndPrepare(harness);

    var beyond = origin + FPVector3.Right * (skill.ConeRange + FP64.One);
    var enemy = SpawnDummy(harness, beyond, EnemyTeamId, isMinion: true);
    var healthBefore = HealthOf(harness, enemy);

    CastAlongX(harness, origin);

    HealthOf(harness, enemy).Should().Be(healthBefore);
  }

  [Fact]
  public void AFriendlyInTheWedge_IsUntouched() {
    var harness = CreateShroomHarness();
    var origin = LearnAndPrepare(harness);

    var friendly = SpawnDummy(harness, Ahead(origin, 3, 0), CasterTeamId, isMinion: true);
    var healthBefore = HealthOf(harness, friendly);

    CastAlongX(harness, origin);

    HealthOf(harness, friendly).Should().Be(healthBefore);
  }

  [Fact]
  public void AnEnemyTurretInTheWedge_IsUntouched() {
    var harness = CreateShroomHarness();
    var origin = LearnAndPrepare(harness);

    // Structures are excluded from skill hits by default - see CombatTargeting.IsSkillHittable.
    var turret = SpawnDummy(harness, Ahead(origin, 3, 0), EnemyTeamId, isMinion: false);
    var healthBefore = HealthOf(harness, turret);

    CastAlongX(harness, origin);

    HealthOf(harness, turret).Should().Be(healthBefore);
  }

  // One wedge per cast: the slot starts its own cooldown, so a key held down cannot chain sprays on
  // consecutive ticks.
  [Fact]
  public void ASecondCastInsideTheCooldown_LandsNothing() {
    var harness = CreateShroomHarness();
    var skill = SlobberAsset(harness);
    var origin = LearnAndPrepare(harness);

    var enemy = SpawnDummy(harness, Ahead(origin, 3, 0), EnemyTeamId, isMinion: true);
    var healthBefore = HealthOf(harness, enemy);

    CastAlongX(harness, origin);
    CastAlongX(harness, origin);

    HealthOf(harness, enemy).Should().Be(healthBefore - skill.DamageAtRank(1));
  }

  [Fact]
  public void ALethalSpray_CreditsTheCasterSoTheKillPaysXp() {
    var harness = CreateShroomHarness();
    var xpBefore = harness.Frame
      .GetReadOnly<ExperienceComponent>(harness.FindHero(CasterPlayerId)).Experience;
    var expectedXp = harness.AssetRegistry.Get<XpRulesAsset>().XpPerMinionKill;

    var origin = LearnAndPrepare(harness);
    var enemy = SpawnDummy(harness, Ahead(origin, 3, 0), EnemyTeamId, isMinion: true);
    harness.Frame.Get<Health>(enemy).Current = 1;

    CastAlongX(harness, origin);
    harness.Tick();

    // Kill credit rides Health.LastDamagerUnitId, which ApplyDamage sets; without it DeathSystem
    // resolves no killer and the kill pays nobody.
    harness.Frame.GetReadOnly<ExperienceComponent>(harness.FindHero(CasterPlayerId))
      .Experience.Should().Be(xpBefore + expectedXp);
  }

  [Fact]
  public void CastAimedAtTheCastersOwnFeet_SpraysAlongItsFacingRatherThanNowhere() {
    var harness = CreateShroomHarness();
    var skill = SlobberAsset(harness);
    var origin = LearnAndPrepare(harness);

    var yaw = harness.Frame.GetReadOnly<TransformComponent>(harness.FindHero(CasterPlayerId)).Rotation;
    var ahead = origin + new FPVector3(FP64.Sin(yaw), FP64.Zero, FP64.Cos(yaw)) * FP64.FromInt(3);
    var enemy = SpawnDummy(harness, ahead, EnemyTeamId, isMinion: true);
    var healthBefore = HealthOf(harness, enemy);

    harness.Tick(SimHarness.CastSkillCommand(CasterPlayerId, harness.Frame.Tick + 1, Primary,
      origin.x, origin.z));

    HealthOf(harness, enemy).Should().Be(healthBefore - skill.DamageAtRank(1));
  }

  // --- setup helpers ---

  // The harness defaults every player to Hairy Wizards, whose Primary is empty. Venomous Slobber
  // needs the Shroom row, so go through the real faction-select path instead.
  private static SimHarness CreateShroomHarness() {
    var harness = SimHarness.CreateInitialized(spawnHeroesNow: false);
    harness.Tick(
      SimHarness.SelectFactionCommand(1, 0, AssetIds.FactionShrooms),
      SimHarness.SelectFactionCommand(2, 0, AssetIds.FactionShrooms));

    DisableAutoAttacks(harness);
    return harness;
  }

  // Stripping Combat off the board leaves the cone as the only thing on the map that can deal damage,
  // so a health delta means exactly one thing.
  private static void DisableAutoAttacks(SimHarness harness) {
    var frame = harness.Frame;
    var attackers = new List<EntityRef>();

    var filter = frame.Filter<Combat>();
    while (filter.Next(out var entity))
      attackers.Add(entity);

    foreach (var entity in attackers)
      frame.Remove<Combat>(entity);
  }

  // Learns the slot and returns the position the cast fires from. Read after the upgrade tick and
  // before the cast, because commands run ahead of the Update phase: this is exactly what
  // SkillActions sees, and it is past the tick NavigationAgentSystem snaps the hero onto the mesh.
  private static FPVector3 LearnAndPrepare(SimHarness harness) {
    harness.Tick(SimHarness.UpgradeSkillCommand(CasterPlayerId, 0, Primary));
    return harness.Frame.GetReadOnly<TransformComponent>(harness.FindHero(CasterPlayerId)).Position;
  }

  private static void CastAlongX(SimHarness harness, FPVector3 origin) {
    harness.Tick(SimHarness.CastSkillCommand(CasterPlayerId, harness.Frame.Tick + 1, Primary,
      origin.x + FP64.FromInt(20), origin.z));
  }

  private static FPVector3 Ahead(FPVector3 origin, int forward, int lateral) {
    return origin + new FPVector3(FP64.FromInt(forward), FP64.Zero, FP64.FromInt(lateral));
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

  // --- readers ---

  private static SkillAsset SlobberAsset(SimHarness harness) {
    return harness.AssetRegistry.Get<SkillAsset>(AssetIds.SkillShroomPrimary);
  }

  private static FP64 HealthOf(SimHarness harness, EntityRef entity) {
    return harness.Frame.GetReadOnly<Health>(entity).Current;
  }
}
