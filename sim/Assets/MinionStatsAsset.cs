using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim.Assets {
  [KlothoDataAsset(103, AssetId = 103, Key = "MinionStats")]
  public partial class MinionStatsAsset : IDataAsset {
    [KlothoOrder(0)] public int Health;
    [KlothoOrder(1)] public FP64 MoveSpeed;
    [KlothoOrder(2)] public int AttackDamage;
    [KlothoOrder(3)] public FP64 AttackRange;
    [KlothoOrder(4)] public int AttackCooldownTicks;
  }
}
