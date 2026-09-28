using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim.Assets;

[KlothoDataAsset(AssetIds.TypeIds.Faction)]
public partial class FactionAsset : IDataAsset {
  [KlothoOrder(0)] public int HeroStatsAssetId;
  [KlothoOrder(1)] public int MinionStatsAssetId;
}
