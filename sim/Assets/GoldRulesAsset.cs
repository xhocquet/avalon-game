using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim.Assets;

// Instance id is AssetIds.GoldRules; look it up with Get<GoldRulesAsset>().
// Kill bounties, the gold counterpart to XpRulesAsset - flat across players, a kill is worth what the
// victim is worth. The passive trickle and the starting wallet are not here: they are match pacing,
// and live on MatchRulesAsset beside the match clock they are measured against.
[KlothoDataAsset(AssetIds.TypeIds.GoldRules, AssetId = AssetIds.GoldRules, Key = "GoldRules")]
public partial class GoldRulesAsset : IDataAsset {
  [KlothoOrder(0)] public int GoldPerMinionKill;
  [KlothoOrder(1)] public int GoldPerHeroKill;
  [KlothoOrder(2)] public int GoldPerTurretKill;
  [KlothoOrder(3)] public int GoldPerCrystalKill;

  // Assists are not tracked yet - nothing reads this. See the TODO in README.md.
  [KlothoOrder(4)] public int GoldPerAssist;
}
