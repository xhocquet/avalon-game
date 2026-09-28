using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim.Assets;

[KlothoDataAsset(AssetIds.TypeIds.GoldRules, AssetId = AssetIds.GoldRules, Key = "GoldRules")]
public partial class GoldRulesAsset : IDataAsset {
  [KlothoOrder(0)] public int GoldPerMinionKill;
  [KlothoOrder(1)] public int GoldPerHeroKill;
  [KlothoOrder(2)] public int GoldPerTurretKill;
  [KlothoOrder(3)] public int GoldPerCrystalKill;

  [KlothoOrder(4)] public int GoldPerAssist; // Unused until assists are tracked
}
