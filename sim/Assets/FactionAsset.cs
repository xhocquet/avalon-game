using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim.Assets;

// Instance ids live in the AssetIds.Faction* block; look one up with Get<FactionAsset>(id).
[KlothoDataAsset(AssetIds.TypeIds.Faction)]
public partial class FactionAsset : IDataAsset {
  [KlothoOrder(0)] public int HeroAssetId;
  [KlothoOrder(1)] public int MinionStatsAssetId;
}
