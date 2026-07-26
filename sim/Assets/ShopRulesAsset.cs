using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim.Assets;

// Instance id is AssetIds.ShopRules; look it up with Get<ShopRulesAsset>().
[KlothoDataAsset(AssetIds.TypeIds.ShopRules, AssetId = AssetIds.ShopRules, Key = "ShopRules")]
public partial class ShopRulesAsset : IDataAsset {
  [KlothoOrder(0)] public FP64 InteractRange;
}
