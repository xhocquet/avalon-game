using System.Collections.Generic;
using FluentAssertions;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using Meesles.Avalon.Sim.Factories;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using Xunit;

namespace Meesles.Avalon.Sim.Tests;

// Snailhead's Tertiary: a self-cast disc that lays Harden's defensive buff on the caster and every
// allied hero and minion standing in it. The buff mechanism itself is covered by HardenTests; what is
// exercised here is the ally-side area collect and that the per-unit loop reaches everyone it should.
public class SwivelEyesTests {
  private const int CasterPlayerId = 1;
  private const int CasterTeamId = 1;
  private const int EnemyTeamId = 2;
  private const int Tertiary = (int)SkillSlot.Tertiary;

  [Fact]
  public void Cast_RaisesTheCastersArmorAndMagicResistByTheRowsPercentage() {
    var harness = CreateSnailheadHarness();
    var skill = SwivelEyesAsset(harness);
    var armorBefore = CasterStat(harness, StatType.Armor);
    var resistBefore = CasterStat(harness, StatType.MagicResist);

    LearnAndCast(harness);

    var percent = skill.BuffPercentAtRank(1);
    CasterStat(harness, StatType.Armor).Should().Be(armorBefore + armorBefore * percent);
    CasterStat(harness, StatType.MagicResist).Should().Be(resistBefore + resistBefore * percent);
  }

  // The whole buff is authored in the row: it is self-cast, has a disc, and both defensive entries
  // trace back to it. A hardcoded percentage or radius fails here rather than shipping.
  [Fact]
  public void EveryBuffNumber_TracesBackToTheAssetRow() {
    var harness = CreateSnailheadHarness();
    var skill = SwivelEyesAsset(harness);

    skill.IsSelfCast.Should().BeTrue();
    skill.HasArea.Should().BeTrue();
    skill.BuffDurationMs.Should().BePositive();
    skill.BuffPercent.Should().BeGreaterThan(FP64.Zero);
    skill.BuffPercentAtRank(2).Should().Be(skill.BuffPercent + skill.BuffPercentPerRank);

    var armorBefore = CasterStat(harness, StatType.Armor);
    LearnAndCast(harness);

    var armor = CasterEntries(harness).Should().ContainSingle(e => e.Stat == StatType.Armor).Subject;
    armor.SourceId.Should().Be(AssetIds.SkillSnailheadTertiary);
    armor.Applied.Should().Be(armorBefore * skill.BuffPercentAtRank(1));
    CasterEntries(harness).Should().Contain(e => e.Stat == StatType.MagicResist);
  }

  [Fact]
  public void AnAlliedUnitInsideTheDisc_GetsTheSameBuff() {
    var harness = CreateSnailheadHarness();
    var skill = SwivelEyesAsset(harness);
    var origin = LearnPosition(harness);

    var ally = SpawnDummy(harness, Ahead(origin, 3, 0), CasterTeamId, armor: 20, resist: 12);
    var armorBefore = StatOf(harness, ally, StatType.Armor);
    var resistBefore = StatOf(harness, ally, StatType.MagicResist);

    Cast(harness);

    var percent = skill.BuffPercentAtRank(1);
    StatOf(harness, ally, StatType.Armor).Should().Be(armorBefore + armorBefore * percent);
    StatOf(harness, ally, StatType.MagicResist).Should().Be(resistBefore + resistBefore * percent);
  }

  [Fact]
  public void AnAlliedUnitPastTheDiscsReach_IsUntouched() {
    var harness = CreateSnailheadHarness();
    var origin = LearnPosition(harness);

    var ally = SpawnDummy(harness, Ahead(origin, 20, 0), CasterTeamId, armor: 20, resist: 12);
    var armorBefore = StatOf(harness, ally, StatType.Armor);

    Cast(harness);

    StatOf(harness, ally, StatType.Armor).Should().Be(armorBefore);
    HasBuffs(harness, ally).Should().BeFalse();
  }

  [Fact]
  public void AnEnemyInsideTheDisc_IsUntouched() {
    var harness = CreateSnailheadHarness();
    var origin = LearnPosition(harness);

    var enemy = SpawnDummy(harness, Ahead(origin, 3, 0), EnemyTeamId, armor: 20, resist: 12);
    var armorBefore = StatOf(harness, enemy, StatType.Armor);

    Cast(harness);

    StatOf(harness, enemy, StatType.Armor).Should().Be(armorBefore);
    HasBuffs(harness, enemy).Should().BeFalse();
  }

  // The multi-target path still hands each entry the same fixed expiry, so the caster's copy reverts
  // exactly the way a plain self-buff does.
  [Fact]
  public void TheBuffHoldsForTheAuthoredDurationThenRevertsExactly() {
    var harness = CreateSnailheadHarness();
    var skill = SwivelEyesAsset(harness);
    var armorBefore = CasterStat(harness, StatType.Armor);
    var resistBefore = CasterStat(harness, StatType.MagicResist);

    var castTick = LearnAndCast(harness);
    var frame = harness.Frame;
    var durationTicks = TickMath.MsToTicksCeil(ref frame, skill.BuffDurationMs);

    AdvanceTo(harness, castTick + durationTicks - 1);
    CasterStat(harness, StatType.Armor).Should().BeGreaterThan(armorBefore);

    AdvanceTo(harness, castTick + durationTicks);
    CasterStat(harness, StatType.Armor).Should().Be(armorBefore);
    CasterStat(harness, StatType.MagicResist).Should().Be(resistBefore);
    CasterEntries(harness).Should().BeEmpty();
  }

  // --- setup helpers ---

  private static SimHarness CreateSnailheadHarness() {
    var harness = SimHarness.CreateInitialized(spawnHeroesNow: false);
    harness.Tick(
      SimHarness.SelectFactionCommand(1, 0, AssetIds.FactionSnailheads),
      SimHarness.SelectFactionCommand(2, 0, AssetIds.FactionSnailheads));
    return harness;
  }

  // Learns the slot and returns the tick the cast lands on, which the expiry is measured from.
  private static int LearnAndCast(SimHarness harness) {
    harness.Tick(SimHarness.UpgradeSkillCommand(CasterPlayerId, 0, Tertiary));
    var castTick = harness.Frame.Tick;
    Cast(harness);
    return castTick;
  }

  // Learns the slot and returns the caster position the disc will centre on: read after the upgrade
  // tick and before the cast, past the tick the hero snaps onto the navmesh.
  private static FPVector3 LearnPosition(SimHarness harness) {
    harness.Tick(SimHarness.UpgradeSkillCommand(CasterPlayerId, 0, Tertiary));
    return harness.Frame.GetReadOnly<TransformComponent>(harness.FindHero(CasterPlayerId)).Position;
  }

  private static void Cast(SimHarness harness) {
    harness.Tick(SimHarness.CastSkillCommand(CasterPlayerId, 0, Tertiary));
  }

  private static void AdvanceTo(SimHarness harness, int tick) {
    while (harness.Frame.Tick <= tick)
      harness.Tick();
  }

  private static FPVector3 Ahead(FPVector3 origin, int forward, int lateral) {
    return origin + new FPVector3(FP64.FromInt(forward), FP64.Zero, FP64.FromInt(lateral));
  }

  private static EntityRef SpawnDummy(SimHarness harness, FPVector3 position, int teamId, int armor,
    int resist) {
    var frame = harness.Frame;
    var entity = frame.CreateEntity();

    frame.Add(entity, TransformFactory.At(position));
    frame.Add(entity, new UnitIdentity {
      UnitId = UnitLookup.NextUnitId(ref frame),
      UnitTypeId = SimulationSetup.MinionUnitTypeId
    });
    frame.Add(entity, new Team { TeamId = teamId });
    frame.Add(entity, new Minion { WaveId = 99 });
    frame.Add(entity, new Health(FP64.FromInt(500)));
    frame.Add(entity, Stats.Create()
      .With(StatType.Armor, FP64.FromInt(armor))
      .With(StatType.MagicResist, FP64.FromInt(resist)));

    return entity;
  }

  // --- readers ---

  private static SkillAsset SwivelEyesAsset(SimHarness harness) {
    return harness.AssetRegistry.Get<SkillAsset>(AssetIds.SkillSnailheadTertiary);
  }

  private static FP64 CasterStat(SimHarness harness, StatType stat) {
    return StatOf(harness, harness.FindHero(CasterPlayerId), stat);
  }

  private static FP64 StatOf(SimHarness harness, EntityRef entity, StatType stat) {
    return harness.Frame.GetReadOnly<Stats>(entity).Get(stat);
  }

  private static bool HasBuffs(SimHarness harness, EntityRef entity) {
    var frame = harness.Frame;
    return frame.Has<StatBuffs>(entity) &&
           StatBuffApplication.ActiveCount(ref frame, entity) > 0;
  }

  private readonly record struct BuffEntry(int SourceId, StatType Stat, FP64 Applied, int ExpiryTick);

  private static List<BuffEntry> CasterEntries(SimHarness harness) {
    var entries = new List<BuffEntry>();
    var hero = harness.FindHero(CasterPlayerId);
    if (!harness.Frame.Has<StatBuffs>(hero))
      return entries;

    ref readonly var buffs = ref harness.Frame.GetReadOnly<StatBuffs>(hero);
    for (var i = 0; i < StatBuffs.MaxEntries; i++)
      if (buffs.IsActive(i))
        entries.Add(new BuffEntry(buffs.GetSourceId(i), buffs.GetStat(i), buffs.GetApplied(i),
          buffs.GetExpiryTick(i)));

    return entries;
  }
}
