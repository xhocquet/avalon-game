using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using Meesles.Avalon.Sim.Heroes;
using xpTURN.Klotho.Core;
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
    var maxHealth = HealthApplication.GetMaxHealth(ref frame, hero);
    frame.Get<Health>(hero).Current = maxHealth / FP64.FromInt(2);
    StatBuffApplication.ApplyPercent(ref frame, hero, DebuffSource, StatType.MoveSpeed,
      -FP64.Half, 600).Should().BeTrue();
    Snares.Apply(ref frame, hero, DebuffSource, 600).Should().BeTrue();

    var castTick = LearnAndCast(harness);
    AdvanceTo(harness, castTick + Ticks(harness, skill.ChargeDurationMsAtRank(1)) - 1);
    Health(harness).Should().Be(maxHealth / FP64.FromInt(2));
    ActiveBuffs(harness).Should().ContainSingle(e => e.SourceId == DebuffSource);

    AdvanceTo(harness, castTick + Ticks(harness, skill.ChargeDurationMsAtRank(1)));
    Health(harness).Should().Be(maxHealth);
    frame = harness.Frame;
    Snares.IsSnared(ref frame, hero).Should().BeFalse();
    ActiveBuffs(harness).Should().OnlyContain(e => e.SourceId == AssetIds.SkillSnailheadUltimate);
    ActiveBuffs(harness).Select(e => e.Stat).Should().BeEquivalentTo([StatType.Armor, StatType.MagicResist]);
  }

  [Fact]
  public void MovingBeforeCompletion_CancelsTheChannelAndItsEffects() {
    var harness = CreateSnailheadHarness();
    var skill = MoltAsset(harness);
    var hero = Caster(harness);
    var frame = harness.Frame;
    var maxHealth = HealthApplication.GetMaxHealth(ref frame, hero);
    frame.Get<Health>(hero).Current = maxHealth / FP64.FromInt(2);

    var castTick = LearnAndCast(harness);
    harness.Frame.Get<TransformComponent>(hero).Position += FPVector3.Right;
    harness.Tick();

    var frame = harness.Frame;
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
    if (!harness.Frame.Has<StatBuffs>(hero))
      return entries;

    ref readonly var buffs = ref harness.Frame.GetReadOnly<StatBuffs>(hero);
    for (var i = 0; i < StatBuffs.MaxEntries; i++)
      if (buffs.IsActive(i))
        entries.Add(new BuffEntry(buffs.GetSourceId(i), buffs.GetStat(i)));
    return entries;
  }

  private readonly record struct BuffEntry(int SourceId, StatType Stat);
}
