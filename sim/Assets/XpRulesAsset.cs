using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim.Assets;

// Kill rewards and the shared level curve. Hero stat growth lives on HeroStatsAsset.
[KlothoDataAsset(AssetIds.TypeIds.XpRules, AssetId = AssetIds.XpRules, Key = "XpRules")]
public partial class XpRulesAsset : IDataAsset {
  [KlothoOrder(0)] public int XpPerMinionKill;
  [KlothoOrder(1)] public int XpPerHeroKill;
  [KlothoOrder(2)] public int XpPerTurretKill;
  [KlothoOrder(3)] public int XpPerCrystalKill;
  [KlothoOrder(4)] public int MaxLevel;
  [KlothoOrder(5)] public int XpToSecondLevel; // xp needed for level 2
  [KlothoOrder(6)] public int XpPerLevelIncrement; // modifier applied per level for xp req.

  [KlothoOrder(7)] public FP64 StatGrowthCurveA; // A + B * (MaxLevel - 1) must equal 1
  [KlothoOrder(8)] public FP64 StatGrowthCurveB;

  // Lifetime XP required for level.
  public int TotalXpForLevel(int level) {
    if (level <= 1)
      return 0;

    var steps = level - 1;
    return steps * XpToSecondLevel + XpPerLevelIncrement * (steps * (steps - 1) / 2);
  }
}
