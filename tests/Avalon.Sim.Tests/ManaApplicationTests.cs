using FluentAssertions;
using Meesles.Avalon.Sim.Components;
using Xunit;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Tests;

public class ManaApplicationTests {
  [Fact]
  public void Spend_DeductsWhenThePoolCoversIt() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    EntityRef hero = harness.FindHero(1);
    frame.Get<Health>(hero).Mana = Fp(200);

    ManaApplication.TrySpend(ref frame, hero, Fp(75)).Should().BeTrue();
    frame.GetReadOnly<Health>(hero).Mana.Should().Be(Fp(125));
  }

  [Fact]
  public void Spend_FailsAndMovesNothingWhenShort() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    EntityRef hero = harness.FindHero(1);
    frame.Get<Health>(hero).Mana = Fp(50);

    ManaApplication.TrySpend(ref frame, hero, Fp(75)).Should().BeFalse();
    frame.GetReadOnly<Health>(hero).Mana.Should().Be(Fp(50));
  }

  [Fact]
  public void Spend_OfZeroOrLess_AlwaysSucceedsAndSpendsNothing() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    EntityRef hero = harness.FindHero(1);
    frame.Get<Health>(hero).Mana = Fp(10);

    ManaApplication.TrySpend(ref frame, hero, FP64.Zero).Should().BeTrue();
    ManaApplication.TrySpend(ref frame, hero, Fp(-5)).Should().BeTrue();
    frame.GetReadOnly<Health>(hero).Mana.Should().Be(Fp(10));
  }

  [Fact]
  public void CanAfford_TracksTrySpend() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    EntityRef hero = harness.FindHero(1);
    frame.Get<Health>(hero).Mana = Fp(60);

    ManaApplication.CanAfford(ref frame, hero, Fp(60)).Should().BeTrue();
    ManaApplication.CanAfford(ref frame, hero, Fp(61)).Should().BeFalse();
  }

  [Fact]
  public void Restore_StopsAtMaxMana() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    EntityRef hero = harness.FindHero(1);
    var maxMana = frame.GetReadOnly<StatsComponent>(hero).MaxMana;
    frame.Get<Health>(hero).Mana = maxMana - Fp(5);

    var restored = ManaApplication.Restore(ref frame, hero, Fp(500));

    restored.Should().Be(Fp(5));
    frame.GetReadOnly<Health>(hero).Mana.Should().Be(maxMana);
  }

  [Fact]
  public void Restore_BelowMax_RestoresTheFullAmount() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    EntityRef hero = harness.FindHero(1);
    frame.Get<Health>(hero).Mana = Fp(10);

    ManaApplication.Restore(ref frame, hero, Fp(25)).Should().Be(Fp(25));
    frame.GetReadOnly<Health>(hero).Mana.Should().Be(Fp(35));
  }

  [Fact]
  public void RestoreToFull_FillsThePool() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    EntityRef hero = harness.FindHero(1);
    frame.Get<Health>(hero).Mana = FP64.Zero;

    ManaApplication.RestoreToFull(ref frame, hero);

    frame.GetReadOnly<Health>(hero).Mana
      .Should().Be(frame.GetReadOnly<StatsComponent>(hero).MaxMana);
  }

  [Fact]
  public void GrantMaxMana_GrowsThePoolAndTopsCurrentUpByTheSameAmount() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    EntityRef hero = harness.FindHero(1);
    var maxMana = frame.GetReadOnly<StatsComponent>(hero).MaxMana;
    frame.Get<Health>(hero).Mana = maxMana - Fp(40);

    ManaApplication.GrantMaxMana(ref frame, hero, Fp(20));

    frame.GetReadOnly<StatsComponent>(hero).MaxMana.Should().Be(maxMana + Fp(20));
    frame.GetReadOnly<Health>(hero).Mana.Should().Be(maxMana - Fp(20));
  }

  [Fact]
  public void GrantMaxMana_Negative_PullsCurrentDownWithTheShrinkingPool() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    EntityRef hero = harness.FindHero(1);
    var maxMana = frame.GetReadOnly<StatsComponent>(hero).MaxMana;

    ManaApplication.GrantMaxMana(ref frame, hero, -Fp(30));

    frame.GetReadOnly<StatsComponent>(hero).MaxMana.Should().Be(maxMana - Fp(30));
    frame.GetReadOnly<Health>(hero).Mana.Should().Be(maxMana - Fp(30));
  }

  [Fact]
  public void GrantMaxMana_Negative_LeavesCurrentAloneWhenItStillFits() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    EntityRef hero = harness.FindHero(1);
    var maxMana = frame.GetReadOnly<StatsComponent>(hero).MaxMana;
    frame.Get<Health>(hero).Mana = Fp(10);

    ManaApplication.GrantMaxMana(ref frame, hero, -Fp(20));

    frame.GetReadOnly<StatsComponent>(hero).MaxMana.Should().Be(maxMana - Fp(20));
    frame.GetReadOnly<Health>(hero).Mana.Should().Be(Fp(10));
  }

  private static FP64 Fp(int value) => FP64.FromInt(value);
}
