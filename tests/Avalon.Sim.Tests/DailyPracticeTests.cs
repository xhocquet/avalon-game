using System.Linq;
using FluentAssertions;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using Xunit;

namespace Meesles.Avalon.Sim.Tests;

public class DailyPracticeTests {
  private const int PlayerId = 1;
  private const int Secondary = (int)SkillSlot.Secondary;

  [Fact]
  public void Cast_DashesThroughFoesAndAppliesTheAttackSpeedBuff() {
    var harness = CreateSkinwalkerHarness();
    var hero = harness.FindHero(PlayerId);
    var skill = Asset(harness);
    var attackSpeed = harness.Frame.GetReadOnly<Stats>(hero).BonusAttackSpeed;
    harness.Frame.Remove<Combat>(hero);
    var target = SpawnDummy(harness, Position(harness, hero) + FPVector3.Right * FP64.FromInt(2), 2);
    var targetUnitId = harness.Frame.GetReadOnly<UnitIdentity>(target).UnitId;
    var collector = new EventCollector();
    collector.BeginTick(0);
    harness.Frame.EventRaiser = collector;

    LearnAndCast(harness, FPVector3.Right);
    TickUntilDashLands(harness, hero);

    collector.Collected.OfType<AttackHitEvent>().Single(evt =>
      evt.AttackerUnitId == harness.Frame.GetReadOnly<UnitIdentity>(hero).UnitId &&
      evt.TargetUnitId == targetUnitId).Damage.Should().Be(skill.DamageAtRank(1));
    harness.Frame.GetReadOnly<Stats>(hero).BonusAttackSpeed.Should().BeGreaterThan(attackSpeed);
  }

  [Fact]
  public void Cast_UsesTheRemainingDashesWhileTheInitialCooldownRuns() {
    var harness = CreateSkinwalkerHarness();
    var hero = harness.FindHero(PlayerId);
    var skill = Asset(harness);
    var origin = Position(harness, hero);

    LearnAndCast(harness, FPVector3.Right);
    TickUntilDashLands(harness, hero);
    var cooldownAfterFirstDash = harness.Frame.GetReadOnly<Skills>(hero).GetCooldownRemainingTicks(Secondary);
    Dash(harness, FPVector3.Right);
    TickUntilDashLands(harness, hero);
    Dash(harness, FPVector3.Right);
    TickUntilDashLands(harness, hero);

    harness.Frame.Has<SkillDash>(hero).Should().BeFalse();
    harness.Frame.GetReadOnly<Skills>(hero).GetCooldownRemainingTicks(Secondary)
      .Should().BeLessThan(cooldownAfterFirstDash);
    Position(harness, hero).Should().Be(
      origin + FPVector3.Right * skill.DashDistance * FP64.FromInt(3));
  }

  private static SimHarness CreateSkinwalkerHarness() {
    var harness = SimHarness.CreateInitialized(spawnHeroesNow: false);
    harness.Tick(SimHarness.SelectFactionCommand(1, 0, AssetIds.FactionSkinwalkerTribe),
      SimHarness.SelectFactionCommand(2, 0, AssetIds.FactionSkinwalkerTribe));
    return harness;
  }

  private static void LearnAndCast(SimHarness harness, FPVector3 direction) {
    var hero = harness.FindHero(PlayerId);
    harness.Frame.Get<Skills>(hero).TrySpendPoint(Secondary, 4).Should().BeTrue();
    Dash(harness, direction);
  }

  private static void Dash(SimHarness harness, FPVector3 direction) {
    var position = Position(harness, harness.FindHero(PlayerId));
    harness.Tick(SimHarness.CastSkillCommand(PlayerId, 0, Secondary,
      position.x + direction.x * FP64.FromInt(100), position.z + direction.z * FP64.FromInt(100)));
  }

  private static void TickUntilDashLands(SimHarness harness, EntityRef hero) {
    while (harness.Frame.Has<SkillDash>(hero) && harness.Frame.GetReadOnly<SkillDash>(hero).IsActive)
      harness.Tick();
  }

  private static SkillAsset Asset(SimHarness harness) =>
    harness.AssetRegistry.Get<SkillAsset>(AssetIds.SkillSkinwalkerSecondary);

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

  private static FPVector3 Position(SimHarness harness, EntityRef entity) =>
    harness.Frame.GetReadOnly<TransformComponent>(entity).Position;

}
