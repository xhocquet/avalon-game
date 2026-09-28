using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim.Assets;

[KlothoDataAsset(AssetIds.TypeIds.TurretStats, AssetId = AssetIds.TurretStats, Key = "TurretStats")]
public partial class TurretStatsAsset : IDataAsset, IUnitStatsAsset {
  [KlothoOrder(0)] public FP64 Health;
  [KlothoOrder(1)] public FP64 Armor;
  [KlothoOrder(2)] public FP64 MagicResist;
  [KlothoOrder(3)] public FP64 AttackDamage;
  [KlothoOrder(4)] public FP64 AttackSpeed; // Attacks per second
  [KlothoOrder(5)] public FP64 AttackWindup;
  [KlothoOrder(6)] public FP64 AttackRange;

  [KlothoOrder(7)] public FP64 AcquisitionRange; // Turrets cannot chase; keep this equal to AttackRange
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
