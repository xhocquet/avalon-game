using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim.Assets;

// Instance id is AssetIds.PlayerStats; look it up with Get<PlayerStatsAsset>().
[KlothoDataAsset(AssetIds.TypeIds.PlayerStats, AssetId = AssetIds.PlayerStats, Key = "PlayerStats")]
public partial class PlayerStatsAsset : IDataAsset {
  [KlothoOrder(0)] public FP64 MoveSpeed;
  [KlothoOrder(1)] public int Health;
  [KlothoOrder(2)] public FP64 Radius;

  // Passive gold income. The interval is a global cadence (every hero ticks on the same clock), but
  // the amount is only a starting value: it is copied onto Stats.GoldPerTick at spawn so items and
  // buffs can raise a single hero's income without touching anyone else's.
  [KlothoOrder(3)] public int StartingGoldPerTick;
  [KlothoOrder(4)] public int GoldTickIntervalMs;
}
