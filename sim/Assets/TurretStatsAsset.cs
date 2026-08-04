using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim.Assets;

// Instance id is AssetIds.TurretStats; look it up with Get<TurretStatsAsset>().
[KlothoDataAsset(AssetIds.TypeIds.TurretStats, AssetId = AssetIds.TurretStats, Key = "TurretStats")]
public partial class TurretStatsAsset : IDataAsset {
  [KlothoOrder(3)] public int AttackCooldownTicks;
  [KlothoOrder(1)] public int AttackDamage;
  [KlothoOrder(2)] public FP64 AttackRange;
  [KlothoOrder(0)] public int Health;
  [KlothoOrder(4)] public FP64 AttackReacquireRangeMultiplier;
  [KlothoOrder(5)] public int Defense;
}
