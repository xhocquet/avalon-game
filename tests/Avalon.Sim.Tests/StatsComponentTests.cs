using FluentAssertions;
using Meesles.Avalon.Sim.Components;
using Xunit;
using xpTURN.Klotho.Deterministic.Math;

namespace Meesles.Avalon.Sim.Tests;

// The stat block is FP64-backed and clamped, which is what makes a fractional or percentage modifier
// expressible at all. These pin the two properties the old int block did not have.
public class StatsComponentTests {
  [Fact]
  public void Add_KeepsTheFractionalPartOfADelta() {
    var stats = StatsComponent.Create().With(StatType.AttackDamage, FP64.FromInt(68));

    stats.Add(StatType.AttackDamage, FP64.One / FP64.FromInt(2));

    stats.AttackDamage.Should().Be(FP64.FromInt(137) / FP64.FromInt(2)); // 68.5, not 68
  }

  [Fact]
  public void Add_OfAPercentage_ScalesRatherThanTruncating() {
    var stats = StatsComponent.Create().With(StatType.BonusAttackSpeed, FP64.Zero);

    // 2.9% per level, the increment Crystal Giant's row authors.
    var perLevel = FP64.FromInt(29) / FP64.FromInt(1000);
    for (var i = 0; i < 10; i++)
      stats.Add(StatType.BonusAttackSpeed, perLevel);

    stats.BonusAttackSpeed.Should().BeGreaterThan(FP64.FromInt(28) / FP64.FromInt(100));
    stats.BonusAttackSpeed.Should().BeLessThan(FP64.FromInt(30) / FP64.FromInt(100));
  }

  [Fact]
  public void EveryStat_ClampsToItsRangeAtBothEnds() {
    for (var i = 0; i < StatRanges.Count; i++) {
      var stat = (StatType)i;
      var row = StatRanges.Of(stat);

      var high = StatsComponent.Create().With(stat, row.Max + FP64.FromInt(1000));
      high.Get(stat).Should().Be(row.Max, $"{stat} must not exceed its ceiling");

      var low = StatsComponent.Create().With(stat, row.Min - FP64.FromInt(1000));
      low.Get(stat).Should().Be(row.Min, $"{stat} must not fall below its floor");
    }
  }

  // The int block let a large enough negative delta take the pool to zero or below, which reads as
  // dead to everything that checks Health.
  [Fact]
  public void MaxHealth_CanNeverReachZero() {
    var stats = StatsComponent.Create().With(StatType.MaxHealth, FP64.FromInt(650));

    stats.Add(StatType.MaxHealth, -FP64.FromInt(100000));

    stats.MaxHealth.Should().Be(FP64.One);
  }

  // A fresh block is not all zeroes: a zero attack rate would divide by zero in the cooldown.
  [Fact]
  public void Create_SeedsEveryStatInsideItsRange() {
    var stats = StatsComponent.Create();

    for (var i = 0; i < StatRanges.Count; i++) {
      var stat = (StatType)i;
      var row = StatRanges.Of(stat);
      stats.Get(stat).Should().BeGreaterThanOrEqualTo(row.Min);
      stats.Get(stat).Should().BeLessThanOrEqualTo(row.Max);
    }
  }

  [Fact]
  public void AttacksPerSecond_AppliesTheBonusAndHoldsTheCap() {
    var stats = StatsComponent.Create()
      .With(StatType.BaseAttackSpeed, FP64.FromInt(67) / FP64.FromInt(100))
      .With(StatType.BonusAttackSpeed, FP64.One / FP64.FromInt(2));

    // 0.67 * 1.5 = 1.005
    stats.AttacksPerSecond.Should().BeGreaterThan(FP64.One);
    stats.AttacksPerSecond.Should().BeLessThan(FP64.FromInt(101) / FP64.FromInt(100));

    stats.Set(StatType.BonusAttackSpeed, FP64.FromInt(4));
    stats.AttacksPerSecond.Should().Be(FP64.FromInt(5) / FP64.FromInt(2));
  }
}
