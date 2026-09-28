using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim.Assets;

// One collectable resource type. Oasis markers choose which types a map uses.
[KlothoDataAsset(AssetIds.TypeIds.PickupType)]
public partial class PickupTypeAsset : IDataAsset {
  [KlothoOrder(0)] public int Amount; // Resources granted per pickup ejected by an oasis
}
