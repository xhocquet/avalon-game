using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim.Assets;

// Instance id is AssetIds.XpRules; look it up with Get<XpRulesAsset>().
// Kill rates and the level curve are flat across players. Per-level stat gains are not here: they
// are per-hero, on HeroAsset, so two heroes can scale differently off the same curve.
[KlothoDataAsset(AssetIds.TypeIds.XpRules, AssetId = AssetIds.XpRules, Key = "XpRules")]
public partial class XpRulesAsset : IDataAsset {
  [KlothoOrder(0)] public int XpPerMinionKill;
  [KlothoOrder(1)] public int XpPerHeroKill;
  [KlothoOrder(2)] public int XpPerTurretKill;
  [KlothoOrder(3)] public int XpPerCrystalKill;
  [KlothoOrder(4)] public int MaxLevel;
  [KlothoOrder(5)] public int XpToSecondLevel; // xp needed for level 2
  [KlothoOrder(6)] public int XpPerLevelIncrement; // modifier applied per level for xp req.

  // Shapes how a hero's PerLevel growth is spread across the levels: see StatGrowth. The pair must
  // satisfy A + B * (MaxLevel - 1) == 1 so a stat lands exactly on base + growth at the cap;
  // StatGrowthTests pins that.
  [KlothoOrder(7)] public FP64 StatGrowthCurveA;
  [KlothoOrder(8)] public FP64 StatGrowthCurveB;

  // Lifetime XP a hero must have earned to be at `level`
  public int TotalXpForLevel(int level) {
    if (level <= 1)
      return 0;

    var steps = level - 1;
    return steps * XpToSecondLevel + XpPerLevelIncrement * (steps * (steps - 1) / 2);
  }
}
