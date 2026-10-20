using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using Xunit;

namespace Meesles.Avalon.Sim.Tests;

public class MoltTests {
  private const int CasterPlayerId = 1;
  private const int Ultimate = (int)SkillSlot.Ultimate;
  private const int DebuffSource = 4242;

  [Fact]
  public void CompletingTheChannel_HealsCleansesAndAppliesTheRowsDefensiveBuff() {
    var harness = CreateSnailheadHarness();
    var skill = MoltAsset(harness);
    var hero = Caster(harness);
    var frame = harness.Frame;
    var maxHealth = HealthController.GetMaxHealth(ref frame, hero);
    frame.Get<Health>(hero).Current = maxHealth / FP64.FromInt(2);
    BuffsController.Apply(ref frame, hero, DebuffSource, StatType.MoveSpeed,
      FP64.Zero, -FP64.Half, 600).Should().BeTrue();
    SnareController.Apply(ref frame, hero, DebuffSource, 600).Should().BeTrue();

    var castTick = LearnAndCast(harness);
    AdvanceTo(harness, castTick + Ticks(harness, skill.ChargeDurationMsAtRank(1)) - 1);
    Health(harness).Should().Be(maxHealth / FP64.FromInt(2));
    ActiveBuffs(harness).Should().ContainSingle(e => e.SourceId == DebuffSource);

    AdvanceTo(harness, castTick + Ticks(harness, skill.ChargeDurationMsAtRank(1)));
    Health(harness).Should().Be(maxHealth);
    frame = harness.Frame;
    SnareController.IsSnared(ref frame, hero).Should().BeFalse();
    ActiveBuffs(harness).Should().OnlyContain(e => e.SourceId == AssetIds.SkillSnailheadUltimate);
    ActiveBuffs(harness).Select(e => e.Stat).Should().BeEquivalentTo([StatType.Armor, StatType.MagicResist]);
  }

  [Fact]
  public void CastingWhileMoving_StopsTheOrderAndCompletesTheChannel() {
    var harness = CreateSnailheadHarness();
    var hero = Caster(harness);
    var frame = harness.Frame;
    var maxHealth = HealthController.GetMaxHealth(ref frame, hero);
    frame.Get<Health>(hero).Current = maxHealth / FP64.FromInt(2);
    harness.Tick(SimHarness.UpgradeSkillCommand(CasterPlayerId, 0, Ultimate));
    var initialPosition = frame.GetReadOnly<TransformComponent>(hero).Position;
    var target = initialPosition + FPVector3.Right * FP64.FromInt(10);
    harness.Tick(SimHarness.MoveCommand(CasterPlayerId, 0, target.x, target.z));
    frame.Has<UnitMoveTarget>(hero).Should().BeTrue();
    var castPosition = frame.GetReadOnly<TransformComponent>(hero).Position;
    castPosition.Should().NotBe(initialPosition);

    var castTick = frame.Tick;
    harness.Tick(SimHarness.CastSkillCommand(CasterPlayerId, 0, Ultimate));
    frame.Has<UnitMoveTarget>(hero).Should().BeFalse();
    frame.GetReadOnly<TransformComponent>(hero).Position.Should().Be(castPosition);
    var completeTick = castTick + Ticks(harness, MoltAsset(harness).ChargeDurationMsAtRank(1));
    AdvanceTo(harness, completeTick - 1);
    frame.GetReadOnly<SkillChannel>(hero).IsActive.Should().BeTrue();
    frame.GetReadOnly<TransformComponent>(hero).Position.Should().Be(castPosition);
    AdvanceTo(harness, completeTick);
    Health(harness).Should().Be(maxHealth);
    ActiveBuffs(harness).Should().HaveCount(2);
  }

  [Fact]
  public void CastingWithAnAttackOrder_ClearsPursuit() {
    var harness = CreateSnailheadHarness();
    var hero = Caster(harness);
    var enemy = harness.FindHero(2);
    var frame = harness.Frame;
    UnitIntentController.SetAttackTarget(ref frame, hero, UnitLookup.GetUnitId(ref frame, enemy));
    UnitIntentController.SetMoveTarget(ref frame, hero, frame.GetReadOnly<TransformComponent>(enemy).Position);

    LearnAndCast(harness);

    frame.Has<AttackTargetUnitId>(hero).Should().BeFalse();
    frame.Has<UnitMoveTarget>(hero).Should().BeFalse();
    harness.Tick();
    frame.GetReadOnly<SkillChannel>(hero).IsActive.Should().BeTrue();
  }

  [Fact]
  public void NewMovementOrderAfterCasting_StillCancelsTheChannel() {
    var harness = CreateSnailheadHarness();
    var hero = Caster(harness);
    LearnAndCast(harness);
    var frame = harness.Frame;
    var target = frame.GetReadOnly<TransformComponent>(hero).Position + FPVector3.Right * FP64.FromInt(10);

    harness.Tick(SimHarness.MoveCommand(CasterPlayerId, 0, target.x, target.z));
    harness.Tick();

    frame.GetReadOnly<SkillChannel>(hero).IsActive.Should().BeFalse();
    ActiveBuffs(harness).Should().BeEmpty();
  }

  [Fact]
  public void MovingBeforeCompletion_CancelsTheChannelAndItsEffects() {
    var harness = CreateSnailheadHarness();
    var skill = MoltAsset(harness);
    var hero = Caster(harness);
    var frame = harness.Frame;
    var maxHealth = HealthController.GetMaxHealth(ref frame, hero);
    frame.Get<Health>(hero).Current = maxHealth / FP64.FromInt(2);

    var castTick = LearnAndCast(harness);
    harness.Frame.Get<TransformComponent>(hero).Position += FPVector3.Right;
    harness.Tick();

    frame = harness.Frame;
    frame.GetReadOnly<SkillChannel>(hero).IsActive.Should().BeFalse();
    AdvanceTo(harness, castTick + Ticks(harness, skill.ChargeDurationMsAtRank(1)));
    Health(harness).Should().Be(maxHealth / FP64.FromInt(2));
    ActiveBuffs(harness).Should().BeEmpty();
  }

  private static SimHarness CreateSnailheadHarness() {
    var harness = SimHarness.CreateInitialized(spawnHeroesNow: false);
    harness.AssetRegistry.Get<WaveRulesAsset>().MinionsPerWave = 0;
    harness.Tick(
      SimHarness.SelectFactionCommand(1, 0, AssetIds.FactionSnailheads),
      SimHarness.SelectFactionCommand(2, 0, AssetIds.FactionSnailheads));
    return harness;
  }

  private static int LearnAndCast(SimHarness harness) {
    harness.Tick(SimHarness.UpgradeSkillCommand(CasterPlayerId, 0, Ultimate));
    var castTick = harness.Frame.Tick;
    harness.Tick(SimHarness.CastSkillCommand(CasterPlayerId, 0, Ultimate));
    return castTick;
  }

  private static void AdvanceTo(SimHarness harness, int tick) {
    while (harness.Frame.Tick <= tick)
      harness.Tick();
  }

  private static EntityRef Caster(SimHarness harness) => harness.FindHero(CasterPlayerId);

  private static SkillAsset MoltAsset(SimHarness harness) {
    return harness.AssetRegistry.Get<SkillAsset>(AssetIds.SkillSnailheadUltimate);
  }

  private static FP64 Health(SimHarness harness) {
    return harness.Frame.GetReadOnly<Health>(Caster(harness)).Current;
  }

  private static int Ticks(SimHarness harness, int milliseconds) {
    var frame = harness.Frame;
    return TickMath.MsToTicksCeil(ref frame, milliseconds);
  }

  private static List<BuffEntry> ActiveBuffs(SimHarness harness) {
    var entries = new List<BuffEntry>();
    var hero = Caster(harness);
    foreach (var entry in BuffTestQueries.StatChanges(harness.Frame, hero))
      entries.Add(new BuffEntry(entry.SourceId, entry.Stat));
    return entries;
  }

  private readonly record struct BuffEntry(int SourceId, StatType Stat);
}
