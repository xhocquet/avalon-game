using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim.Assets;

[KlothoDataAsset(AssetIds.TypeIds.ShopItem)]
public partial class ShopItemAsset : IDataAsset {
  [KlothoOrder(0)] public int Cost;
  [KlothoOrder(1)] public FP64 AttackBonus;
}
