using FluentAssertions;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Deterministic.Math;
using Xunit;

namespace Meesles.Avalon.Sim.Tests;

public class StatusEffectsTests {
  private const int SourceId = 4242;

  [Fact]
  public void Death_ClearsEveryNegativeStatusTogether() {
    var harness = SimHarness.CreateInitialized();
    var hero = harness.FindHero(playerId: 1);
    var frame = harness.Frame;
    var baseSpeed = frame.GetReadOnly<Stats>(hero).MoveSpeed;
    var baseArmor = frame.GetReadOnly<Stats>(hero).Armor;

    StatBuffApplication.ApplyPercent(ref frame, hero, SourceId, StatType.MoveSpeed,
      -FP64.Half, 600).Should().BeTrue();
    StatBuffApplication.ApplyPercent(ref frame, hero, SourceId, StatType.Armor,
      -FP64.Half, 600).Should().BeTrue();
    StatBuffApplication.ApplyPercent(ref frame, hero, SourceId + 1, StatType.MoveSpeed,
      FP64.Half, 600).Should().BeTrue();
    Snares.Apply(ref frame, hero, SourceId, 600).Should().BeTrue();
    Silences.Apply(ref frame, hero, SourceId, 600).Should().BeTrue();
    DamageOverTimes.Apply(ref frame, hero, hero, SourceId, FP64.FromInt(10), 600).Should().BeTrue();
    StatusEffects.HasAnyNegative(ref frame, hero).Should().BeTrue();

    frame.Get<Health>(hero).Current = FP64.Zero;
    harness.Tick();

    frame = harness.Frame;
    StatusEffects.HasAnyNegative(ref frame, hero).Should().BeFalse();
    StatBuffApplication.ActiveCount(ref frame, hero).Should().Be(0);
    Snares.IsSnared(ref frame, hero).Should().BeFalse();
    Silences.IsSilenced(ref frame, hero).Should().BeFalse();
    DamageOverTimes.IsBurning(ref frame, hero).Should().BeFalse();
    frame.GetReadOnly<Stats>(hero).MoveSpeed.Should().Be(baseSpeed);
    frame.GetReadOnly<Stats>(hero).Armor.Should().Be(baseArmor);
  }
}
