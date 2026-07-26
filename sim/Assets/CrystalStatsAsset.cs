using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim.Assets;

[KlothoDataAsset(107, AssetId = 107, Key = "CrystalStats")]
public partial class CrystalStatsAsset : IDataAsset {
  [KlothoOrder(0)] public int Health;
}
