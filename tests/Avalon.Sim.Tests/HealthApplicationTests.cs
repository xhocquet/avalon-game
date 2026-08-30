using FluentAssertions;
using Meesles.Avalon.Sim.Components;
using Xunit;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Tests;

public class HealthApplicationTests {
  [Fact]
  public void Heal_StopsAtMaxHealth() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    EntityRef hero = harness.FindHero(1);
    var maxHealth = frame.GetReadOnly<Stats>(hero).MaxHealth;
    frame.Get<Health>(hero).Current = maxHealth - Fp(5);

    var healed = HealthApplication.ApplyHeal(ref frame, hero, Fp(500));

    healed.Should().Be(Fp(5));
    frame.GetReadOnly<Health>(hero).Current.Should().Be(maxHealth);
  }

  [Fact]
  public void Heal_AtFullHealth_IsANoOp() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    EntityRef hero = harness.FindHero(1);
    var maxHealth = frame.GetReadOnly<Stats>(hero).MaxHealth;

    HealthApplication.ApplyHeal(ref frame, hero, Fp(50)).Should().Be(FP64.Zero);
    frame.GetReadOnly<Health>(hero).Current.Should().Be(maxHealth);
  }

  [Fact]
  public void Heal_BelowMax_RestoresTheFullAmount() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    EntityRef hero = harness.FindHero(1);
    frame.Get<Health>(hero).Current = Fp(10);

    HealthApplication.ApplyHeal(ref frame, hero, Fp(25)).Should().Be(Fp(25));
    frame.GetReadOnly<Health>(hero).Current.Should().Be(Fp(35));
  }

  [Fact]
  public void Heal_DoesNotReviveAUnitAtZero() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    EntityRef hero = harness.FindHero(1);
    frame.Get<Health>(hero).Current = FP64.Zero;

    HealthApplication.ApplyHeal(ref frame, hero, Fp(100)).Should().Be(FP64.Zero);
    frame.GetReadOnly<Health>(hero).Current.Should().Be(FP64.Zero);
    frame.GetReadOnly<Health>(hero).IsAlive.Should().BeFalse();
  }

  [Fact]
  public void RestoreToFull_BringsBackAUnitAtZero() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    EntityRef hero = harness.FindHero(1);
    frame.Get<Health>(hero).Current = FP64.Zero;

    HealthApplication.RestoreToFull(ref frame, hero);

    frame.GetReadOnly<Health>(hero).Current
      .Should().Be(frame.GetReadOnly<Stats>(hero).MaxHealth);
  }

  [Fact]
  public void GrantMaxHealth_GrowsThePoolAndTopsCurrentUpByTheSameAmount() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    EntityRef hero = harness.FindHero(1);
    var maxHealth = frame.GetReadOnly<Stats>(hero).MaxHealth;
    frame.Get<Health>(hero).Current = maxHealth - Fp(40);

    HealthApplication.GrantMaxHealth(ref frame, hero, Fp(20));

    frame.GetReadOnly<Stats>(hero).MaxHealth.Should().Be(maxHealth + Fp(20));
    frame.GetReadOnly<Health>(hero).Current.Should().Be(maxHealth - Fp(20));
  }

  [Fact]
  public void GrantMaxHealth_OnADeadUnit_GrowsThePoolWithoutReviving() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    EntityRef hero = harness.FindHero(1);
    var maxHealth = frame.GetReadOnly<Stats>(hero).MaxHealth;
    frame.Get<Health>(hero).Current = FP64.Zero;

    HealthApplication.GrantMaxHealth(ref frame, hero, Fp(20));

    frame.GetReadOnly<Stats>(hero).MaxHealth.Should().Be(maxHealth + Fp(20));
    frame.GetReadOnly<Health>(hero).Current.Should().Be(FP64.Zero);
  }

  [Fact]
  public void GrantMaxHealth_Negative_PullsCurrentDownButNeverKills() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    EntityRef hero = harness.FindHero(1);
    var maxHealth = frame.GetReadOnly<Stats>(hero).MaxHealth;

    HealthApplication.GrantMaxHealth(ref frame, hero, -maxHealth);

    frame.GetReadOnly<Stats>(hero).MaxHealth.Should().Be(FP64.One); // StatRanges floors the pool at 1
    frame.GetReadOnly<Health>(hero).Current.Should().Be(FP64.One);
    frame.GetReadOnly<Health>(hero).IsAlive.Should().BeTrue();
  }

  [Fact]
  public void GrantMaxHealth_Negative_LeavesCurrentAloneWhenItStillFits() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    EntityRef hero = harness.FindHero(1);
    var maxHealth = frame.GetReadOnly<Stats>(hero).MaxHealth;
    frame.Get<Health>(hero).Current = Fp(10);

    HealthApplication.GrantMaxHealth(ref frame, hero, -Fp(20));

    frame.GetReadOnly<Stats>(hero).MaxHealth.Should().Be(maxHealth - Fp(20));
    frame.GetReadOnly<Health>(hero).Current.Should().Be(Fp(10));
  }

  private static FP64 Fp(int value) => FP64.FromInt(value);
}
