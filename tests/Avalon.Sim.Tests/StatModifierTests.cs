using FluentAssertions;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Deterministic.Math;
using Xunit;

namespace Meesles.Avalon.Sim.Tests;

public class StatModifierTests {
  private const int FirstSkill = AssetIds.SkillCrystalGiantSecondary;
  private const int SecondSkill = AssetIds.SkillSnailheadTertiary;

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public void Percentages_AddAgainstTheBase_RegardlessOfApplicationAndRefreshOrder(bool reverse) {
    var harness = CreateWithArmor(FP64.FromInt(100));
    var first = FP64.One / FP64.FromInt(5);
    var second = FP64.Half;
    Apply(harness, reverse ? SecondSkill : FirstSkill, BuffMode.Percent, reverse ? second : first);
    Apply(harness, reverse ? FirstSkill : SecondSkill, BuffMode.Percent, reverse ? first : second);
    var expected = FP64.FromInt(100) + FP64.FromInt(100) * (first + second);
    Armor(harness).Should().Be(expected);
    Armor(harness).ToFloat().Should().Be(170f);

    for (var i = 0; i < 20; i++) {
      Apply(harness, i % 2 == 0 ? FirstSkill : SecondSkill, BuffMode.Percent, i % 2 == 0 ? first : second);
      Armor(harness).Should().Be(expected);
      harness.Tick();
    }
  }

  [Fact]
  public void Percentages_AreSummedBeforeFixedPointMultiplication() {
    var harness = CreateWithArmor(FP64.FromRaw(3));
    Apply(harness, FirstSkill, BuffMode.Percent, FP64.Half);
    Apply(harness, SecondSkill, BuffMode.Percent, FP64.Half);
    Armor(harness).Should().Be(FP64.FromRaw(6));
  }

  [Fact]
  public void Expiry_RebuildsFromTheRemainingPercentageTotal() {
    var harness = CreateWithArmor(FP64.FromInt(100));
    Apply(harness, FirstSkill, BuffMode.Percent, FP64.Half, 2);
    Apply(harness, SecondSkill, BuffMode.Percent, FP64.One / FP64.FromInt(4), 10);
    Armor(harness).Should().Be(FP64.FromInt(175));

    AdvanceTo(harness, 3);
    Armor(harness).Should().Be(FP64.FromInt(125));
    AdvanceTo(harness, 11);
    Armor(harness).Should().Be(FP64.FromInt(100));
    harness.Frame.Has<BaseStats>(harness.FindHero(1)).Should().BeFalse();
  }

  [Fact]
  public void Cleanse_RemovesOnlyTheHarmfulPercentage_WithoutChangingTheBase() {
    var harness = CreateWithArmor(FP64.FromInt(100));
    var frame = harness.Frame;
    var hero = harness.FindHero(1);
    BuffsController.Apply(ref frame, hero, FirstSkill, [
      new(StatType.Armor, BuffMode.Percent, FP64.Half, FP64.Zero),
      new(StatType.Armor, BuffMode.Percent, -FP64.One / FP64.FromInt(4), FP64.Zero)
    ], 1, 100).Should().BeTrue();
    Armor(harness).Should().Be(FP64.FromInt(125));

    BuffsController.ClearHarmful(ref frame, hero).Should().Be(1);
    Armor(harness).Should().Be(FP64.FromInt(150));
    StatsController.GetBase(ref frame, hero, StatType.Armor).Should().Be(FP64.FromInt(100));
    BuffsController.ClearAll(ref frame, hero);
    Armor(harness).Should().Be(FP64.FromInt(100));
    BuffsController.ClearHarmful(ref frame, hero).Should().Be(0);
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public void FlatBuffs_AreAddedAfterPercentages_AndOnlyTheFinalResultIsClamped(bool reverse) {
    var harness = CreateWithArmor(FP64.FromInt(900));
    if (reverse) {
      Apply(harness, SecondSkill, BuffMode.Percent, -FP64.Half, 10);
      Apply(harness, FirstSkill, BuffMode.Flat, FP64.FromInt(500), 2);
    }
    else {
      Apply(harness, FirstSkill, BuffMode.Flat, FP64.FromInt(500), 2);
      Apply(harness, SecondSkill, BuffMode.Percent, -FP64.Half, 10);
    }
    Armor(harness).Should().Be(FP64.FromInt(950));

    AdvanceTo(harness, 3);
    Armor(harness).Should().Be(FP64.FromInt(450));
    AdvanceTo(harness, 11);
    Armor(harness).Should().Be(FP64.FromInt(900));
  }

  [Fact]
  public void CappedBuffs_RetainTheirFullModifiersUntilRemoved() {
    var harness = CreateWithArmor(FP64.FromInt(900));
    Apply(harness, FirstSkill, BuffMode.Percent, FP64.Half, 2);
    Apply(harness, SecondSkill, BuffMode.Percent, FP64.Half, 10);
    Armor(harness).Should().Be(StatRanges.Of(StatType.Armor).Max);

    AdvanceTo(harness, 3);
    Armor(harness).Should().Be(StatRanges.Of(StatType.Armor).Max);
    AdvanceTo(harness, 11);
    Armor(harness).Should().Be(FP64.FromInt(900));
  }

  [Fact]
  public void IndependentStacks_AddTheirPercentages_AndRefreshAtTheCapDoesNotCompound() {
    var harness = CreateWithArmor(FP64.FromInt(100));
    var frame = harness.Frame;
    var hero = harness.FindHero(1);
    var policy = new BuffApplicationPolicy {
      Reapplication = BuffReapplication.Stack,
      MaxStacks = 2,
      Overflow = BuffStackOverflow.RefreshOldest
    };
    for (var i = 0; i < 10; i++) {
      BuffsController.Apply(ref frame, hero, FirstSkill,
        [new(StatType.Armor, BuffMode.Percent, FP64.Half, FP64.Zero)], 1, 100,
        applicationPolicy: policy).Should().BeTrue();
      Armor(harness).Should().Be(FP64.FromInt(i == 0 ? 150 : 200));
    }
    BuffsController.ClearAll(ref frame, hero);
    Armor(harness).Should().Be(FP64.FromInt(100));
  }

  [Fact]
  public void PermanentChanges_UpdateTheBaseAndActiveModifiersImmediately() {
    var harness = CreateWithArmor(FP64.FromInt(100));
    Apply(harness, FirstSkill, BuffMode.Percent, FP64.Half);
    var frame = harness.Frame;
    var hero = harness.FindHero(1);
    StatsController.AddBase(ref frame, hero, StatType.Armor, FP64.FromInt(40));
    Armor(harness).Should().Be(FP64.FromInt(210));
    Apply(harness, FirstSkill, BuffMode.Percent, FP64.Half);
    Armor(harness).Should().Be(FP64.FromInt(210));

    BuffsController.ClearAll(ref frame, hero);
    Armor(harness).Should().Be(FP64.FromInt(140));
    StatsController.AddBase(ref frame, hero, StatType.Armor, FP64.FromInt(10));
    Apply(harness, FirstSkill, BuffMode.Percent, FP64.Half);
    Armor(harness).Should().Be(FP64.FromInt(225));
  }

  [Fact]
  public void ZeroBaseModifiers_RespondToLaterGrowth_AndNegativeBasesClassifyByTheirEffect() {
    var harness = CreateWithArmor(FP64.Zero);
    Apply(harness, FirstSkill, BuffMode.Percent, FP64.Half);
    var frame = harness.Frame;
    var hero = harness.FindHero(1);
    StatsController.AddBase(ref frame, hero, StatType.Armor, FP64.FromInt(100));
    Armor(harness).Should().Be(FP64.FromInt(150));
    BuffsController.ClearHarmful(ref frame, hero).Should().Be(0);

    StatsController.SetBase(ref frame, hero, StatType.Armor, FP64.FromInt(-50));
    Armor(harness).Should().Be(FP64.FromInt(-75));
    BuffsController.ClearHarmful(ref frame, hero).Should().Be(1);
    Armor(harness).Should().Be(FP64.FromInt(-50));
  }

  [Fact]
  public void MaxPoolGrants_UpdatePermanentPoolsUnderActivePercentageModifiers() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    var hero = harness.FindHero(1);
    StatsController.SetBase(ref frame, hero, StatType.MaxHealth, FP64.FromInt(1000));
    StatsController.SetBase(ref frame, hero, StatType.MaxMana, FP64.FromInt(500));
    BuffsController.Apply(ref frame, hero, FirstSkill, [
      new(StatType.MaxHealth, BuffMode.Percent, FP64.Half, FP64.Zero),
      new(StatType.MaxMana, BuffMode.Percent, FP64.Half, FP64.Zero)
    ], 1, 100).Should().BeTrue();

    HealthController.GrantMaxHealth(ref frame, hero, FP64.FromInt(100));
    ManaController.GrantMaxMana(ref frame, hero, FP64.FromInt(100));
    frame.GetReadOnly<Stats>(hero).MaxHealth.Should().Be(FP64.FromInt(1650));
    frame.GetReadOnly<Stats>(hero).MaxMana.Should().Be(FP64.FromInt(900));
    BuffsController.ClearAll(ref frame, hero);
    frame.GetReadOnly<Stats>(hero).MaxHealth.Should().Be(FP64.FromInt(1100));
    frame.GetReadOnly<Stats>(hero).MaxMana.Should().Be(FP64.FromInt(600));
  }

  [Fact]
  public void RefreshAtFullCapacity_PreservesOtherPercentageModifiers() {
    var harness = CreateWithArmor(FP64.FromInt(100));
    for (var i = 0; i < BuffChanges.MaxEntries; i++)
      Apply(harness, FirstSkill + i, BuffMode.Percent, FP64.One / FP64.FromInt(4));
    Armor(harness).Should().Be(FP64.FromInt(250));
    for (var i = 0; i < BuffChanges.MaxEntries; i++) {
      Apply(harness, FirstSkill + i, BuffMode.Percent, FP64.One / FP64.FromInt(4));
      Armor(harness).Should().Be(FP64.FromInt(250));
    }
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public void Rollback_RestoresBaseGrowthPercentagesAndCleanse(bool clearAll) {
    var rollback = RollbackHarness.Create();
    rollback.Advance(5, beforeTick: ApplyAuthoritative);
    var serverFrame = rollback.Server.Frame;
    var baseBefore = StatsController.GetBase(ref serverFrame, rollback.Server.FindHero(1), StatType.Armor);
    rollback.MispredictAndRollback(8, null, beforeTick: sim => {
      var frame = sim.Frame;
      var hero = sim.FindHero(1);
      StatsController.AddBase(ref frame, hero, StatType.Armor, FP64.FromInt(50));
      if (clearAll)
        BuffsController.ClearAll(ref frame, hero);
      else
        BuffsController.ClearHarmful(ref frame, hero);
      Apply(sim, FirstSkill, BuffMode.Percent, FP64.FromInt(2));
    });
    rollback.InSync.Should().BeTrue();
    var clientFrame = rollback.Client.Frame;
    StatsController.GetBase(ref clientFrame, rollback.Client.FindHero(1), StatType.Armor).Should().Be(baseBefore);
    rollback.AdvanceAndCompare(40, beforeTick: ApplyAuthoritative).Should().BeEmpty();
  }

  private static void ApplyAuthoritative(SimHarness harness) {
    var frame = harness.Frame;
    var hero = harness.FindHero(1);
    StatsController.AddBase(ref frame, hero, StatType.Armor, FP64.One);
    Apply(harness, FirstSkill, BuffMode.Percent, FP64.Half);
    Apply(harness, SecondSkill, BuffMode.Percent, -FP64.One / FP64.FromInt(4));
  }

  private static SimHarness CreateWithArmor(FP64 value) {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    StatsController.SetBase(ref frame, harness.FindHero(1), StatType.Armor, value);
    return harness;
  }

  private static void Apply(SimHarness harness, int skillId, BuffMode mode, FP64 magnitude, int duration = 100) {
    var frame = harness.Frame;
    BuffsController.Apply(ref frame, harness.FindHero(1), skillId,
      [new(StatType.Armor, mode, magnitude, FP64.Zero)], 1, duration).Should().BeTrue();
  }

  private static FP64 Armor(SimHarness harness) =>
    harness.Frame.GetReadOnly<Stats>(harness.FindHero(1)).Armor;

  private static void AdvanceTo(SimHarness harness, int tick) {
    while (harness.Frame.Tick < tick)
      harness.Tick();
  }
}
