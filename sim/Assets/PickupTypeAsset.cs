using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim.Assets;

// Instance ids live in the AssetIds.PickupType* block; look one up with Get<PickupTypeAsset>(id).
// One row per collectable resource kind. Which kinds a round actually uses is a property of the
// map, not of this table: an oasis marker names the type it ejects (see SimulationSetup.SpawnOases).
[KlothoDataAsset(AssetIds.TypeIds.PickupType)]
public partial class PickupTypeAsset : IDataAsset {
  [KlothoOrder(0)] public int Amount; // Resources granted per pickup ejected by an oasis
}
