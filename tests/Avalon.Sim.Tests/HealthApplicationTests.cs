using FluentAssertions;
using Meesles.Avalon.Sim.Components;
using Xunit;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Tests;

public class HealthApplicationTests {
  [Fact]
  public void Heal_StopsAtMaxHealth() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    EntityRef hero = harness.FindHero(1);
    int maxHealth = frame.GetReadOnly<StatsComponent>(hero).MaxHealth;
    frame.Get<Health>(hero).Current = maxHealth - 5;

    int healed = HealthApplication.ApplyHeal(ref frame, hero, 500);

    healed.Should().Be(5);
    frame.GetReadOnly<Health>(hero).Current.Should().Be(maxHealth);
  }

  [Fact]
  public void Heal_AtFullHealth_IsANoOp() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    EntityRef hero = harness.FindHero(1);
    int maxHealth = frame.GetReadOnly<StatsComponent>(hero).MaxHealth;

    HealthApplication.ApplyHeal(ref frame, hero, 50).Should().Be(0);
    frame.GetReadOnly<Health>(hero).Current.Should().Be(maxHealth);
  }

  [Fact]
  public void Heal_BelowMax_RestoresTheFullAmount() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    EntityRef hero = harness.FindHero(1);
    frame.Get<Health>(hero).Current = 10;

    HealthApplication.ApplyHeal(ref frame, hero, 25).Should().Be(25);
    frame.GetReadOnly<Health>(hero).Current.Should().Be(35);
  }

  [Fact]
  public void Heal_DoesNotReviveAUnitAtZero() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    EntityRef hero = harness.FindHero(1);
    frame.Get<Health>(hero).Current = 0;

    HealthApplication.ApplyHeal(ref frame, hero, 100).Should().Be(0);
    frame.GetReadOnly<Health>(hero).Current.Should().Be(0);
    frame.GetReadOnly<Health>(hero).IsAlive.Should().BeFalse();
  }

  [Fact]
  public void RestoreToFull_BringsBackAUnitAtZero() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    EntityRef hero = harness.FindHero(1);
    frame.Get<Health>(hero).Current = 0;

    HealthApplication.RestoreToFull(ref frame, hero);

    frame.GetReadOnly<Health>(hero).Current
      .Should().Be(frame.GetReadOnly<StatsComponent>(hero).MaxHealth);
  }

  [Fact]
  public void GrantMaxHealth_GrowsThePoolAndTopsCurrentUpByTheSameAmount() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    EntityRef hero = harness.FindHero(1);
    int maxHealth = frame.GetReadOnly<StatsComponent>(hero).MaxHealth;
    frame.Get<Health>(hero).Current = maxHealth - 40;

    HealthApplication.GrantMaxHealth(ref frame, hero, 20);

    frame.GetReadOnly<StatsComponent>(hero).MaxHealth.Should().Be(maxHealth + 20);
    frame.GetReadOnly<Health>(hero).Current.Should().Be(maxHealth - 20);
  }

  [Fact]
  public void GrantMaxHealth_OnADeadUnit_GrowsThePoolWithoutReviving() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    EntityRef hero = harness.FindHero(1);
    int maxHealth = frame.GetReadOnly<StatsComponent>(hero).MaxHealth;
    frame.Get<Health>(hero).Current = 0;

    HealthApplication.GrantMaxHealth(ref frame, hero, 20);

    frame.GetReadOnly<StatsComponent>(hero).MaxHealth.Should().Be(maxHealth + 20);
    frame.GetReadOnly<Health>(hero).Current.Should().Be(0);
  }

  [Fact]
  public void GrantMaxHealth_Negative_PullsCurrentDownButNeverKills() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    EntityRef hero = harness.FindHero(1);
    int maxHealth = frame.GetReadOnly<StatsComponent>(hero).MaxHealth;

    HealthApplication.GrantMaxHealth(ref frame, hero, -maxHealth);

    frame.GetReadOnly<StatsComponent>(hero).MaxHealth.Should().Be(0);
    frame.GetReadOnly<Health>(hero).Current.Should().Be(1);
    frame.GetReadOnly<Health>(hero).IsAlive.Should().BeTrue();
  }

  [Fact]
  public void GrantMaxHealth_Negative_LeavesCurrentAloneWhenItStillFits() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    EntityRef hero = harness.FindHero(1);
    int maxHealth = frame.GetReadOnly<StatsComponent>(hero).MaxHealth;
    frame.Get<Health>(hero).Current = 10;

    HealthApplication.GrantMaxHealth(ref frame, hero, -20);

    frame.GetReadOnly<StatsComponent>(hero).MaxHealth.Should().Be(maxHealth - 20);
    frame.GetReadOnly<Health>(hero).Current.Should().Be(10);
  }
}
