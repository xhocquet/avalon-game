using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim.Assets;

[KlothoDataAsset(106, AssetId = 106, Key = "TurretStats")]
public partial class TurretStatsAsset : IDataAsset {
  [KlothoOrder(3)] public int AttackCooldownTicks;
  [KlothoOrder(1)] public int AttackDamage;
  [KlothoOrder(2)] public FP64 AttackRange;
  [KlothoOrder(0)] public int Health;
}
