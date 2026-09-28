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

    BuffsController.Apply(ref frame, hero, SourceId, StatType.MoveSpeed,
      FP64.Zero, -FP64.Half, 600).Should().BeTrue();
    BuffsController.Apply(ref frame, hero, SourceId, StatType.Armor,
      FP64.Zero, -FP64.Half, 600).Should().BeTrue();
    BuffsController.Apply(ref frame, hero, SourceId + 1, StatType.MoveSpeed,
      FP64.Zero, FP64.Half, 600).Should().BeTrue();
    SnareController.Apply(ref frame, hero, SourceId, 600).Should().BeTrue();
    SilenceController.Apply(ref frame, hero, SourceId, 600).Should().BeTrue();
    DamageOverTimeController.Apply(ref frame, hero, hero, SourceId, FP64.FromInt(10), 600).Should().BeTrue();
    ActiveBuffCount(harness).Should().Be(3);

    frame.Get<Health>(hero).Current = FP64.Zero;
    harness.Tick();

    frame = harness.Frame;
    ActiveBuffCount(harness).Should().Be(0);
    SnareController.IsSnared(ref frame, hero).Should().BeFalse();
    SilenceController.IsSilenced(ref frame, hero).Should().BeFalse();
    DamageOverTimeController.IsBurning(ref frame, hero).Should().BeFalse();
    frame.GetReadOnly<Stats>(hero).MoveSpeed.Should().Be(baseSpeed);
    frame.GetReadOnly<Stats>(hero).Armor.Should().Be(baseArmor);
  }

  private static int ActiveBuffCount(SimHarness harness) {
    var frame = harness.Frame;
    var hero = harness.FindHero(1);
    if (!frame.Has<StatBuffs>(hero))
      return 0;

    ref readonly var buffs = ref frame.GetReadOnly<StatBuffs>(hero);
    var count = 0;
    for (var i = 0; i < StatBuffs.MaxEntries; i++)
      if (buffs.IsActive(i))
        count++;
    return count;
  }
}
