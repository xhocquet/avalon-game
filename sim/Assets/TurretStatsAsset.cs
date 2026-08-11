using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim.Assets;

// Instance id is AssetIds.TurretStats; look it up with Get<TurretStatsAsset>().
[KlothoDataAsset(AssetIds.TypeIds.TurretStats, AssetId = AssetIds.TurretStats, Key = "TurretStats")]
public partial class TurretStatsAsset : IDataAsset, IUnitStatsAsset {
  [KlothoOrder(0)] public FP64 Health;
  [KlothoOrder(1)] public FP64 Armor;
  [KlothoOrder(2)] public FP64 MagicResist;
  [KlothoOrder(3)] public FP64 AttackDamage;
  [KlothoOrder(4)] public FP64 AttackSpeed; // Attacks per second
  [KlothoOrder(5)] public FP64 AttackWindup;
  [KlothoOrder(6)] public FP64 AttackRange;

  // A turret cannot chase, so it is authored equal to AttackRange rather than reaching past it.
  [KlothoOrder(7)] public FP64 AcquisitionRange;
  [KlothoOrder(8)] public FP64 GameplayRadius;

  FP64 IUnitStatsAsset.BaseHealth => Health;
  FP64 IUnitStatsAsset.BaseMana => FP64.Zero;
  FP64 IUnitStatsAsset.BaseHealthRegen => FP64.Zero;
  FP64 IUnitStatsAsset.BaseManaRegen => FP64.Zero;
  FP64 IUnitStatsAsset.BaseArmor => Armor;
  FP64 IUnitStatsAsset.BaseMagicResist => MagicResist;
  FP64 IUnitStatsAsset.BaseAttackDamage => AttackDamage;
  FP64 IUnitStatsAsset.BaseAttackSpeed => AttackSpeed;
  FP64 IUnitStatsAsset.CritChance => FP64.Zero;
  FP64 IUnitStatsAsset.CritDamage => FP64.Zero;
  FP64 IUnitStatsAsset.MoveSpeed => FP64.Zero; // Turrets don't move
  FP64 IUnitStatsAsset.AttackRange => AttackRange;
  FP64 IUnitStatsAsset.AcquisitionRange => AcquisitionRange;
  FP64 IUnitStatsAsset.AttackWindup => AttackWindup;
  FP64 IUnitStatsAsset.GameplayRadius => GameplayRadius;
  FP64 IUnitStatsAsset.PathingRadius => FP64.Zero; // No nav agent
}
