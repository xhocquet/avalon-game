using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim.Assets;

[KlothoDataAsset(105)]
public partial class ShopItemAsset : IDataAsset {
  [KlothoOrder(0)] public int Cost;
  [KlothoOrder(1)] public int AttackBonus;
}
