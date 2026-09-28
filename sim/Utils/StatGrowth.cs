using Meesles.Avalon.Sim.Assets;
using xpTURN.Klotho.Deterministic.Math;

namespace Meesles.Avalon.Sim;

// How a hero's per-level stat growth is spread across the levels.
//   value(level) = base + growth * n * (A + B * n),  n = level - 1
public static class StatGrowth {
  // How many levels' worth of growth have arrived by `level` - (MaxLevel - 1) at the cap.
  public static FP64 Factor(XpRulesAsset rules, int level) {
    if (level <= 1)
      return FP64.Zero;

    var n = FP64.FromInt(level - 1);
    return n * (rules.StatGrowthCurveA + rules.StatGrowthCurveB * n);
  }

  // ReSharper disable once UnusedMember.Global
  public static FP64 AtLevel(XpRulesAsset rules, FP64 baseValue, FP64 growth, int level) =>
    baseValue + growth * Factor(rules, level);

  // What a stat gains moving between two levels
  public static FP64 Between(XpRulesAsset rules, FP64 growth, int fromLevel, int toLevel) =>
    growth * (Factor(rules, toLevel) - Factor(rules, fromLevel));
}
