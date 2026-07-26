using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim.Assets;

// Instance id is AssetIds.CrystalStats; look it up with Get<CrystalStatsAsset>().
[KlothoDataAsset(AssetIds.TypeIds.CrystalStats, AssetId = AssetIds.CrystalStats, Key = "CrystalStats")]
public partial class CrystalStatsAsset : IDataAsset {
  [KlothoOrder(0)] public int Health;
}
