using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim.Assets;

// Instance id is AssetIds.CrystalStats; look it up with Get<CrystalStatsAsset>().
// A crystal only takes damage, so everything on the attacking half reads as zero - it implements
// IUnitStatsAsset anyway so CrystalFactory builds its stats through the same seeded path as
// everything else, rather than hand-writing fields and inheriting whatever the defaults happen to be.
[KlothoDataAsset(AssetIds.TypeIds.CrystalStats, AssetId = AssetIds.CrystalStats, Key = "CrystalStats")]
public partial class CrystalStatsAsset : IDataAsset, IUnitStatsAsset {
  [KlothoOrder(0)] public FP64 Health;
  [KlothoOrder(1)] public FP64 Armor;
  [KlothoOrder(2)] public FP64 MagicResist;
  [KlothoOrder(3)] public FP64 GameplayRadius;

  FP64 IUnitStatsAsset.BaseHealth => Health;
  FP64 IUnitStatsAsset.BaseMana => FP64.Zero;
  FP64 IUnitStatsAsset.BaseHealthRegen => FP64.Zero;
  FP64 IUnitStatsAsset.BaseManaRegen => FP64.Zero;
  FP64 IUnitStatsAsset.BaseArmor => Armor;
  FP64 IUnitStatsAsset.BaseMagicResist => MagicResist;
  FP64 IUnitStatsAsset.BaseAttackDamage => FP64.Zero;
  FP64 IUnitStatsAsset.BaseAttackSpeed => FP64.Zero;
  FP64 IUnitStatsAsset.CritChance => FP64.Zero;
  FP64 IUnitStatsAsset.CritDamage => FP64.Zero;
  FP64 IUnitStatsAsset.MoveSpeed => FP64.Zero;
  FP64 IUnitStatsAsset.AttackRange => FP64.Zero;
  FP64 IUnitStatsAsset.AcquisitionRange => FP64.Zero;
  FP64 IUnitStatsAsset.AttackWindup => FP64.Zero;
  FP64 IUnitStatsAsset.GameplayRadius => GameplayRadius;
  FP64 IUnitStatsAsset.PathingRadius => FP64.Zero;
}
