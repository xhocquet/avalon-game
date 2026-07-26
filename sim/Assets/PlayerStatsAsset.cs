using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim.Assets;

// Instance id is AssetIds.PlayerStats; look it up with Get<PlayerStatsAsset>().
[KlothoDataAsset(AssetIds.TypeIds.PlayerStats, AssetId = AssetIds.PlayerStats, Key = "PlayerStats")]
public partial class PlayerStatsAsset : IDataAsset {
  [KlothoOrder(0)] public FP64 MoveSpeed;
  [KlothoOrder(1)] public FP64 MatchDuration;
  [KlothoOrder(2)] public int Health;
  [KlothoOrder(3)] public FP64 Radius;
}
