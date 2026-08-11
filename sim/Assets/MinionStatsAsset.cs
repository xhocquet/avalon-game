using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim.Assets;

// Instance id is AssetIds.MinionStats; look it up with Get<MinionStatsAsset>().
// Minions never level, so this is the flat half of IUnitStatsAsset with no growth pairs.
[KlothoDataAsset(AssetIds.TypeIds.MinionStats, AssetId = AssetIds.MinionStats, Key = "MinionStats")]
public partial class MinionStatsAsset : IDataAsset, IUnitStatsAsset {
  [KlothoOrder(0)] public FP64 Health;
  [KlothoOrder(1)] public FP64 Armor;
  [KlothoOrder(2)] public FP64 MagicResist;
  [KlothoOrder(3)] public FP64 AttackDamage;
  [KlothoOrder(4)] public FP64 AttackSpeed; // Attacks per second
  [KlothoOrder(5)] public FP64 AttackWindup;
  [KlothoOrder(6)] public FP64 MoveSpeed;
  [KlothoOrder(7)] public FP64 AttackRange;
  [KlothoOrder(8)] public FP64 AcquisitionRange;
  [KlothoOrder(9)] public FP64 GameplayRadius;
  [KlothoOrder(10)] public FP64 PathingRadius;

  FP64 IUnitStatsAsset.BaseHealth => Health;
  FP64 IUnitStatsAsset.BaseMana => FP64.Zero; // No skills to spend it on
  FP64 IUnitStatsAsset.BaseHealthRegen => FP64.Zero;
  FP64 IUnitStatsAsset.BaseManaRegen => FP64.Zero;
  FP64 IUnitStatsAsset.BaseArmor => Armor;
  FP64 IUnitStatsAsset.BaseMagicResist => MagicResist;
  FP64 IUnitStatsAsset.BaseAttackDamage => AttackDamage;
  FP64 IUnitStatsAsset.BaseAttackSpeed => AttackSpeed;
  FP64 IUnitStatsAsset.CritChance => FP64.Zero;
  FP64 IUnitStatsAsset.CritDamage => FP64.Zero; // Clamped up to the CritDamage floor
  FP64 IUnitStatsAsset.MoveSpeed => MoveSpeed;
  FP64 IUnitStatsAsset.AttackRange => AttackRange;
  FP64 IUnitStatsAsset.AcquisitionRange => AcquisitionRange;
  FP64 IUnitStatsAsset.AttackWindup => AttackWindup;
  FP64 IUnitStatsAsset.GameplayRadius => GameplayRadius;
  FP64 IUnitStatsAsset.PathingRadius => PathingRadius;
}
