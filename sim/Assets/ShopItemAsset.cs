using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim.Assets;

// Instance ids live in the AssetIds.ShopItem* block; look one up with Get<ShopItemAsset>(id).
[KlothoDataAsset(AssetIds.TypeIds.ShopItem)]
public partial class ShopItemAsset : IDataAsset {
  [KlothoOrder(0)] public int Cost;
  [KlothoOrder(1)] public FP64 AttackBonus;
}
