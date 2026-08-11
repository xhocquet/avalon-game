using FluentAssertions;
using Meesles.Avalon.Sim.Components;
using Xunit;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Tests;

// Mitigation reads Armor or MagicResist off StatsComponent depending on the damage type, and both
// ends of the curve matter: the int block passed damage through unchanged whenever the resist went
// negative.
public class DamageMitigationTests {
  [Fact]
  public void MagicalDamage_MitigatesAgainstMagicResistNotArmor() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    var target = harness.FindHero(1);
    SetResists(ref frame, target, armor: 100, magicResist: 0);

    var damage = FP64.FromInt(100);
    DamageApplication.Mitigate(ref frame, target, damage, DamageType.Physical)
      .Should().Be(FP64.FromInt(50));
    DamageApplication.Mitigate(ref frame, target, damage, DamageType.Magical)
      .Should().Be(damage);
  }

  // Amplification is the curve mirrored, so it approaches 2x rather than jumping there and never
  // flips the sign of the hit.
  // Expectations are what a 1000-damage hit becomes.
  [Theory]
  [InlineData(-100, 1500)] // 2 - 100/200 = 1.5x
  [InlineData(-50, 1333)] // 2 - 100/150 = 1.333x
  public void NegativeResist_AmplifiesInsteadOfPassingDamageThrough(int armor, int expectedDamage) {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    var target = harness.FindHero(1);
    SetResists(ref frame, target, armor, magicResist: 0);

    var mitigated = DamageApplication.Mitigate(ref frame, target, FP64.FromInt(1000));

    var expected = FP64.FromInt(expectedDamage);
    FP64.Abs(mitigated - expected).Should().BeLessThanOrEqualTo(FP64.One,
      $"expected about {expected} but got {mitigated}");
  }

  // A fractional hit still costs the target something, rather than rounding away to nothing.
  [Fact]
  public void ASmallHit_FloorsAtOneRatherThanVanishing() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    var target = harness.FindHero(1);
    SetResists(ref frame, target, armor: 900, magicResist: 0);

    DamageApplication.Mitigate(ref frame, target, FP64.One / FP64.FromInt(2))
      .Should().Be(FP64.One);
  }

  [Fact]
  public void ZeroResist_LeavesTheHitAlone() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    var target = harness.FindHero(1);
    SetResists(ref frame, target, armor: 0, magicResist: 0);

    var damage = FP64.FromInt(137) / FP64.FromInt(2);
    DamageApplication.Mitigate(ref frame, target, damage).Should().Be(damage);
  }

  private static void SetResists(ref Frame frame, EntityRef entity, int armor, int magicResist) {
    ref var stats = ref frame.Get<StatsComponent>(entity);
    stats.Set(StatType.Armor, FP64.FromInt(armor));
    stats.Set(StatType.MagicResist, FP64.FromInt(magicResist));
  }
}
