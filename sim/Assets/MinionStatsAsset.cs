using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim.Assets;

// Instance id is AssetIds.MinionStats; look it up with Get<MinionStatsAsset>().
[KlothoDataAsset(AssetIds.TypeIds.MinionStats, AssetId = AssetIds.MinionStats, Key = "MinionStats")]
public partial class MinionStatsAsset : IDataAsset, IUnitStatsAsset {
  [KlothoOrder(0)] public int Health;
  [KlothoOrder(1)] public FP64 MoveSpeed;
  [KlothoOrder(2)] public int AttackDamage;
  [KlothoOrder(3)] public FP64 AttackRange;
  [KlothoOrder(4)] public int AttackCooldownTicks;
  [KlothoOrder(5)] public FP64 AttackReacquireRangeMultiplier;
  [KlothoOrder(6)] public FP64 Radius;
  [KlothoOrder(7)] public int Defense;

  int IUnitStatsAsset.Health => Health;
  int IUnitStatsAsset.AttackDamage => AttackDamage;
  int IUnitStatsAsset.Defense => Defense;
  int IUnitStatsAsset.AttackCooldownTicks => AttackCooldownTicks;
  FP64 IUnitStatsAsset.MoveSpeed => MoveSpeed;
  FP64 IUnitStatsAsset.AttackRange => AttackRange;
  FP64 IUnitStatsAsset.AttackReacquireRangeMultiplier => AttackReacquireRangeMultiplier;
}
