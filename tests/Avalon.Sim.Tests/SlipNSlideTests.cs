using FluentAssertions;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using Xunit;

namespace Meesles.Avalon.Sim.Tests;

public class SlipNSlideTests {
  private const int CasterPlayerId = 1;
  private const int Primary = (int)SkillSlot.Primary;

  [Fact]
  public void Cast_DashesAtTheRowsSpeedAndHealsAlliesInItsPath() {
    var harness = CreatePickleKnightHarness();
    var skill = Asset(harness);
    var hero = harness.FindHero(CasterPlayerId);
    var origin = Position(harness, hero);
    var alongPath = SpawnDummy(harness, origin + FPVector3.Right * FP64.FromInt(3), teamId: 1);
    var offPath = SpawnDummy(harness, origin + FPVector3.Forward * FP64.FromInt(3), teamId: 1);
    SetHealth(harness, alongPath, FP64.One);
    SetHealth(harness, offPath, FP64.One);

    LearnAndCast(harness, origin + FPVector3.Right * FP64.FromInt(100));

    var firstStep = skill.DashSpeed * (FP64.FromInt(SimHarness.DefaultDeltaTimeMs) / FP64.FromInt(1000));
    Position(harness, hero).Should().Be(origin + FPVector3.Right * firstStep);
    Health(harness, alongPath).Should().Be(FP64.One);

    while (harness.Frame.Has<SkillDash>(hero))
      harness.Tick();

    Position(harness, hero).Should().Be(origin + FPVector3.Right * skill.DashDistance);
    Health(harness, alongPath).Should().Be(FP64.One + SkillAsset.AtRank(skill.HealAmount, skill.HealAmountPerRank, 1));
    Health(harness, offPath).Should().Be(FP64.One);
  }

  [Fact]
  public void Cast_AppliesTheRowsDefensiveBuffToTheCaster() {
    var harness = CreatePickleKnightHarness();
    var skill = Asset(harness);
    var hero = harness.FindHero(CasterPlayerId);
    var armor = harness.Frame.GetReadOnly<Stats>(hero).Armor;
    var magicResist = harness.Frame.GetReadOnly<Stats>(hero).MagicResist;

    LearnAndCast(harness, Position(harness, hero) + FPVector3.Right);

    harness.Frame.GetReadOnly<Stats>(hero).Armor.Should().Be(armor + armor * SkillAsset.AtRank(skill.BuffPercent, skill.BuffPercentPerRank, 1));
    harness.Frame.GetReadOnly<Stats>(hero).MagicResist.Should().Be(magicResist + magicResist * SkillAsset.AtRank(skill.BuffPercent, skill.BuffPercentPerRank, 1));
  }

  private static SimHarness CreatePickleKnightHarness() {
    var harness = SimHarness.CreateInitialized(spawnHeroesNow: false);
    harness.Tick(SimHarness.SelectFactionCommand(1, 0, AssetIds.FactionPickleKnights),
      SimHarness.SelectFactionCommand(2, 0, AssetIds.FactionPickleKnights));
    return harness;
  }

  private static void LearnAndCast(SimHarness harness, FPVector3 target) {
    var hero = harness.FindHero(CasterPlayerId);
    harness.Frame.Get<Skills>(hero).TrySpendPoint(Primary, 4).Should().BeTrue();
    harness.Tick(SimHarness.CastSkillCommand(CasterPlayerId, 0, Primary, target.x, target.z));
  }

  private static SkillAsset Asset(SimHarness harness) {
    return harness.AssetRegistry.Get<SkillAsset>(AssetIds.SkillPickleKnightPrimary);
  }

  private static EntityRef SpawnDummy(SimHarness harness, FPVector3 position, int teamId) {
    var frame = harness.Frame;
    var entity = frame.CreateEntity();
    frame.Add(entity, new TransformComponent { Position = position, Scale = FPVector3.One });
    frame.Add(entity, new UnitIdentity {
      UnitId = UnitLookup.NextUnitId(ref frame),
      UnitTypeId = SimulationSetup.MinionUnitTypeId
    });
    frame.Add(entity, new Team(teamId));
    frame.Add(entity, new Minion { WaveId = 0 });
    var stats = Stats.Create();
    stats.Set(StatType.MaxHealth, FP64.FromInt(500));
    frame.Add(entity, new Health(500));
    frame.Add(entity, stats);
    return entity;
  }

  private static void SetHealth(SimHarness harness, EntityRef entity, FP64 health) {
    harness.Frame.Get<Health>(entity).Current = health;
  }

  private static FP64 Health(SimHarness harness, EntityRef entity) {
    return harness.Frame.GetReadOnly<Health>(entity).Current;
  }

  private static FPVector3 Position(SimHarness harness, EntityRef entity) {
    return harness.Frame.GetReadOnly<TransformComponent>(entity).Position;
  }
}
