using FluentAssertions;
using Meesles.Avalon.Sim.Components;
using Xunit;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Tests;

public class CheatTests {
  [Fact]
  public void SetCheatCommand_EnablesGodModeForTheIssuingPlayerOnly() {
    var harness = SimHarness.CreateInitialized();

    harness.Tick(SimHarness.SetCheatCommand(1, harness.Frame.Tick, CheatFlags.GodMode));

    var frame = harness.Frame;
    Cheats.IsEnabled(ref frame, 1, CheatFlags.GodMode).Should().BeTrue();
    Cheats.IsEnabled(ref frame, 2, CheatFlags.GodMode).Should().BeFalse();
  }

  [Fact]
  public void SetCheatCommand_ClearsWithEnabledZero() {
    var harness = SimHarness.CreateInitialized();

    harness.Tick(SimHarness.SetCheatCommand(1, harness.Frame.Tick, CheatFlags.GodMode));
    harness.Tick(SimHarness.SetCheatCommand(1, harness.Frame.Tick, CheatFlags.GodMode, false));

    var frame = harness.Frame;
    Cheats.IsEnabled(ref frame, 1, CheatFlags.GodMode).Should().BeFalse();
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
    Cheats.AreAllEnabled(ref frame, 1, CheatFlags.GodMode).Should().BeFalse();
    Cheats.AreAllEnabled(ref frame, 1, CheatFlags.None).Should().BeTrue();

    harness.Tick(SimHarness.SetCheatCommand(1, harness.Frame.Tick, CheatFlags.GodMode));

    frame = harness.Frame;
    Cheats.AreAllEnabled(ref frame, 1, CheatFlags.GodMode).Should().BeTrue();
    Cheats.AreAllEnabled(ref frame, 2, CheatFlags.GodMode).Should().BeFalse();
  }

  [Fact]
  public void GodMode_HeroTakesNoDamage() {
    var harness = SimHarness.CreateInitialized();
    harness.Tick(SimHarness.SetCheatCommand(1, harness.Frame.Tick, CheatFlags.GodMode));

    var frame = harness.Frame;
    EntityRef hero = harness.FindHero(1);
    EntityRef attacker = harness.FindHero(2);
    int before = frame.GetReadOnly<Health>(hero).Current;

    DamageApplication.ApplyDamage(ref frame, attacker, hero, 5000).Should().Be(0);

    frame.GetReadOnly<Health>(hero).Current.Should().Be(before);
    frame.GetReadOnly<Health>(hero).LastDamagerUnitId.Should().Be(0);
  }

  [Fact]
  public void GodMode_DoesNotProtectOtherPlayers() {
    var harness = SimHarness.CreateInitialized();
    harness.Tick(SimHarness.SetCheatCommand(1, harness.Frame.Tick, CheatFlags.GodMode));

    var frame = harness.Frame;
    EntityRef target = harness.FindHero(2);
    int before = frame.GetReadOnly<Health>(target).Current;

    DamageApplication.ApplyDamage(ref frame, harness.FindHero(1), target, 10)
      .Should().BeGreaterThan(0);
    frame.GetReadOnly<Health>(target).Current.Should().BeLessThan(before);
  }
}
