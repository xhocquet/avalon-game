using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim.Assets;

// Multi-instance catalog asset: one instance per purchasable shop item, keyed by its own AssetId
// (like FactionAsset). No AssetId or Key named arg on the attribute — those are singleton-lookup
// keys, and a catalog has many instances of this one type. Callers fetch a specific item via
// Get<ShopItemAsset>(itemId). Item ids live in the 300 range to stay clear of the singleton assets
// (100-103) and the faction catalog (200 range).
[KlothoDataAsset(105)]
public partial class ShopItemAsset : IDataAsset {
  // Gold price to purchase this item from the shop.
  [KlothoOrder(0)] public int Cost;

  // Flat attack bonus granted to the buyer while the item is owned.
  [KlothoOrder(1)] public int AttackBonus;
}
