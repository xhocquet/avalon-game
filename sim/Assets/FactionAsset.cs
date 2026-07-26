using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim.Assets;

[KlothoDataAsset(104)]
public partial class FactionAsset : IDataAsset {
  [KlothoOrder(0)] public int ChampionUnitTypeId;
  [KlothoOrder(1)] public int MinionUnitTypeId;
  [KlothoOrder(2)] public int MinionStatsAssetId;
}
