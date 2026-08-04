using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim.Assets;

// Instance id is AssetIds.XpRules; look it up with Get<XpRulesAsset>().
[KlothoDataAsset(AssetIds.TypeIds.XpRules, AssetId = AssetIds.XpRules, Key = "XpRules")]
public partial class XpRulesAsset : IDataAsset {
  [KlothoOrder(0)] public int XpPerMinionKill;
  [KlothoOrder(1)] public int XpPerHeroKill;
  [KlothoOrder(2)] public int XpPerTurretKill;
  [KlothoOrder(3)] public int XpPerCrystalKill;

  [KlothoOrder(4)] public int MaxLevel;

  // Level 2 costs XpToSecondLevel; every level after costs XpPerLevelIncrement more than the one
  // before it. Integer-only so the curve is identical on every peer.
  [KlothoOrder(5)] public int XpToSecondLevel;
  [KlothoOrder(6)] public int XpPerLevelIncrement;

  // Applied through Stats.Add on each level gained.
  [KlothoOrder(7)] public int MaxHealthPerLevel;
  [KlothoOrder(8)] public int StrengthPerLevel;
  [KlothoOrder(9)] public FP64 AttackSpeedPerLevel;

  // Lifetime XP a hero must have earned to be at `level`. Closed form of the arithmetic series above
  // rather than a loop, so the cost of a UI progress bar does not scale with MaxLevel.
  public int TotalXpForLevel(int level) {
    if (level <= 1)
      return 0;

    var steps = level - 1;
    return steps * XpToSecondLevel + XpPerLevelIncrement * (steps * (steps - 1) / 2);
  }
}
