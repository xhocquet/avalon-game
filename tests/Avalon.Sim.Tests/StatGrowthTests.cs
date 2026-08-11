using FluentAssertions;
using Meesles.Avalon.Sim.Assets;
using Xunit;
using xpTURN.Klotho.Deterministic.Math;

namespace Meesles.Avalon.Sim.Tests;

// The growth curve is what lets a hero row be authored as a level-1 value and a per-level gain and
// still land on a known number at the cap.
public class StatGrowthTests {
  // If this drifts, every authored PerLevel silently means something else at the cap.
  [Fact]
  public void CurveConstants_MakeTheRampReachOneAtTheLevelCap() {
    var rules = SimHarness.CreateInitialized().AssetRegistry.Get<XpRulesAsset>();

    var ramp = rules.StatGrowthCurveA + rules.StatGrowthCurveB * FP64.FromInt(rules.MaxLevel - 1);

    FP64.Abs(ramp - FP64.One).Should().BeLessThanOrEqualTo(FP64.One / FP64.FromInt(100000),
      $"A + B * (MaxLevel - 1) must be 1, but is {ramp}");
  }

  [Fact]
  public void Factor_IsZeroAtLevelOneAndTheLevelSpanAtTheCap() {
    var rules = SimHarness.CreateInitialized().AssetRegistry.Get<XpRulesAsset>();

    StatGrowth.Factor(rules, 1).Should().Be(FP64.Zero);
    StatGrowth.Factor(rules, 0).Should().Be(FP64.Zero);

    var atCap = StatGrowth.Factor(rules, rules.MaxLevel);
    FP64.Abs(atCap - FP64.FromInt(rules.MaxLevel - 1))
      .Should().BeLessThanOrEqualTo(FP64.One / FP64.FromInt(1000));
  }

  // Back-loaded on purpose: a level late in the game is worth more than an early one. A flat curve
  // would put the halfway point at exactly half.
  [Fact]
  public void Growth_ArrivesBackLoadedRatherThanFlat() {
    var rules = SimHarness.CreateInitialized().AssetRegistry.Get<XpRulesAsset>();
    var halfway = (rules.MaxLevel + 1) / 2;

    var arrived = StatGrowth.Factor(rules, halfway);
    var flat = FP64.FromInt(halfway - 1);

    arrived.Should().BeLessThan(flat);
  }

  // The Crystal Giant row is authored straight off a published stat block, so reproducing its
  // maxima is the check that the curve and the authored PerLevel values agree. Everything is in
  // tenths so the fractional rows stay integer InlineData.
  [Theory]
  [InlineData(6500, 1100, 25200)] // Health 650 - 2520
  [InlineData(3400, 450, 11050)] // Mana 340 - 1105
  [InlineData(370, 39, 1033)] // Armor 37 - 103.3
  [InlineData(320, 20.5, 668.5)] // Magic resist 32 - 66.85
  [InlineData(680, 40, 1360)] // Attack damage 68 - 136
  [InlineData(60, 7.5, 187.5)] // Health regen 6 - 18.75
  [InlineData(75, 6, 177)] // Mana regen 7.5 - 17.7
  public void CrystalGiantMaxima_ComeOutOfTheCurve(double baseTenths, double perLevelTenths,
    double expectedTenths) {
    var rules = SimHarness.CreateInitialized().AssetRegistry.Get<XpRulesAsset>();
    var ten = FP64.FromInt(10);

    var value = StatGrowth.AtLevel(rules, FP64.FromDouble(baseTenths) / ten,
      FP64.FromDouble(perLevelTenths) / ten, rules.MaxLevel);
    var expected = FP64.FromDouble(expectedTenths) / ten;

    FP64.Abs(value - expected).Should().BeLessThanOrEqualTo(FP64.One / FP64.FromInt(1000),
      $"expected {expected} at level {rules.MaxLevel} but got {value}");
  }

  [Fact]
  public void Between_IsTheDifferenceOfTwoLevels() {
    var rules = SimHarness.CreateInitialized().AssetRegistry.Get<XpRulesAsset>();
    var growth = FP64.FromInt(110);

    var between = StatGrowth.Between(rules, growth, 3, 9);
    var difference = StatGrowth.AtLevel(rules, FP64.Zero, growth, 9)
                     - StatGrowth.AtLevel(rules, FP64.Zero, growth, 3);

    between.Should().Be(difference);
  }
}
