using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim.Assets;

// Instance ids live in the AssetIds.Faction* block; look one up with Get<FactionAsset>(id).
[KlothoDataAsset(AssetIds.TypeIds.Faction)]
public partial class FactionAsset : IDataAsset {
  [KlothoOrder(0)] public int ChampionUnitTypeId;
  [KlothoOrder(1)] public int MinionUnitTypeId;
  [KlothoOrder(2)] public int MinionStatsAssetId;
}
