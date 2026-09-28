using FluentAssertions;
using Meesles.Avalon.Sim.Components;
using Xunit;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Tests;

public class CheatTests {
  [Fact]
  public void SetCheatCommand_EnablesGodModeForTheIssuingPlayerOnly() {
    var harness = SimHarness.CreateInitialized();

    harness.Tick(SimHarness.SetCheatCommand(1, harness.Frame.Tick, CheatFlags.GodMode));

    var frame = harness.Frame;
    CheatsController.IsEnabled(ref frame, 1, CheatFlags.GodMode).Should().BeTrue();
    CheatsController.IsEnabled(ref frame, 2, CheatFlags.GodMode).Should().BeFalse();
  }

  [Fact]
  public void SetCheatCommand_ClearsWithEnabledZero() {
    var harness = SimHarness.CreateInitialized();

    harness.Tick(SimHarness.SetCheatCommand(1, harness.Frame.Tick, CheatFlags.GodMode));
    harness.Tick(SimHarness.SetCheatCommand(1, harness.Frame.Tick, CheatFlags.GodMode, false));

    var frame = harness.Frame;
    CheatsController.IsEnabled(ref frame, 1, CheatFlags.GodMode).Should().BeFalse();
  }

  [Fact]
  public void SetCheatCommand_UnknownFlagsAreRejected() {
    var harness = SimHarness.CreateInitialized();

    harness.Tick(SimHarness.SetCheatCommand(1, harness.Frame.Tick, (CheatFlags)0x40000000));

    harness.Count<CheatState>().Should().Be(0);
  }

  [Fact]
  public void AreAllEnabled_RequiresEveryRequestedFlag() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;

    // What the client retries against: no table yet means nothing has been applied.
    CheatsController.AreAllEnabled(ref frame, 1, CheatFlags.GodMode).Should().BeFalse();
    CheatsController.AreAllEnabled(ref frame, 1, CheatFlags.None).Should().BeTrue();

    harness.Tick(SimHarness.SetCheatCommand(1, harness.Frame.Tick, CheatFlags.GodMode));

    frame = harness.Frame;
    CheatsController.AreAllEnabled(ref frame, 1, CheatFlags.GodMode).Should().BeTrue();
    CheatsController.AreAllEnabled(ref frame, 2, CheatFlags.GodMode).Should().BeFalse();
  }

  [Fact]
  public void GodMode_HeroTakesNoDamage() {
    var harness = SimHarness.CreateInitialized();
    harness.Tick(SimHarness.SetCheatCommand(1, harness.Frame.Tick, CheatFlags.GodMode));

    var frame = harness.Frame;
    EntityRef hero = harness.FindHero(1);
    EntityRef attacker = harness.FindHero(2);
    var before = frame.GetReadOnly<Health>(hero).Current;

    DamageController.ApplyDamage(ref frame, attacker, hero, FP64.FromInt(5000)).Should().Be(FP64.Zero);

    frame.GetReadOnly<Health>(hero).Current.Should().Be(before);
    frame.GetReadOnly<Health>(hero).LastDamagerUnitId.Should().Be(0);
  }

  [Fact]
  public void GodMode_DoesNotProtectOtherPlayers() {
    var harness = SimHarness.CreateInitialized();
    harness.Tick(SimHarness.SetCheatCommand(1, harness.Frame.Tick, CheatFlags.GodMode));

    var frame = harness.Frame;
    EntityRef target = harness.FindHero(2);
    var before = frame.GetReadOnly<Health>(target).Current;

    DamageController.ApplyDamage(ref frame, harness.FindHero(1), target, FP64.FromInt(10))
      .Should().BeGreaterThan(FP64.Zero);
    frame.GetReadOnly<Health>(target).Current.Should().BeLessThan(before);
  }
}
