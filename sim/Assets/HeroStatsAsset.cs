using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim.Assets;

// Base is level 1; PerLevel reaches the cap through StatGrowth. Distances are metres.
[KlothoDataAsset(AssetIds.TypeIds.Hero)]
public partial class HeroStatsAsset : IDataAsset, IUnitStatsAsset {
  [KlothoOrder(0)] public int BehaviorId;
  [KlothoOrder(1)] public int SkillSetId;
  [KlothoOrder(2)] public int Skill1AssetId;
  [KlothoOrder(3)] public int Skill2AssetId;
  [KlothoOrder(4)] public int Skill3AssetId;
  [KlothoOrder(5)] public int Skill4AssetId;

  [KlothoOrder(6)] public FP64 BaseHealth;
  [KlothoOrder(7)] public FP64 HealthPerLevel;
  [KlothoOrder(8)] public FP64 BaseMana;
  [KlothoOrder(9)] public FP64 ManaPerLevel;
  [KlothoOrder(10)] public FP64 BaseHealthRegen; // Per 5 seconds
  [KlothoOrder(11)] public FP64 HealthRegenPerLevel;
  [KlothoOrder(12)] public FP64 BaseManaRegen; // Per 5 seconds
  [KlothoOrder(13)] public FP64 ManaRegenPerLevel;
  [KlothoOrder(14)] public FP64 BaseArmor;
  [KlothoOrder(15)] public FP64 ArmorPerLevel;
  [KlothoOrder(16)] public FP64 BaseMagicResist;
  [KlothoOrder(17)] public FP64 MagicResistPerLevel;
  [KlothoOrder(18)] public FP64 BaseAttackDamage;
  [KlothoOrder(19)] public FP64 AttackDamagePerLevel;
  [KlothoOrder(20)] public FP64 BaseAttackSpeed; // Attacks per second
  [KlothoOrder(21)] public FP64 BonusAttackSpeedPerLevel; // Fraction of base; 0.029 is +2.9%

  [KlothoOrder(22)] public FP64 AttackSpeedRatio; // Unused; match BaseAttackSpeed without a separate ratio
  [KlothoOrder(23)] public FP64 AttackWindup;

  [KlothoOrder(24)] public FP64 CritChance;
  [KlothoOrder(25)] public FP64 CritDamage;
  [KlothoOrder(26)] public FP64 MoveSpeed;
  [KlothoOrder(27)] public FP64 AttackRange;
  [KlothoOrder(28)] public FP64 AcquisitionRange;

  [KlothoOrder(29)] public FP64 GameplayRadius;
  [KlothoOrder(30)] public FP64 SelectionRadius; // View-only
  [KlothoOrder(31)] public FP64 PathingRadius;

  FP64 IUnitStatsAsset.BaseHealth => BaseHealth;
  FP64 IUnitStatsAsset.BaseMana => BaseMana;
  FP64 IUnitStatsAsset.BaseHealthRegen => BaseHealthRegen;
  FP64 IUnitStatsAsset.BaseManaRegen => BaseManaRegen;
  FP64 IUnitStatsAsset.BaseArmor => BaseArmor;
  FP64 IUnitStatsAsset.BaseMagicResist => BaseMagicResist;
  FP64 IUnitStatsAsset.BaseAttackDamage => BaseAttackDamage;
  FP64 IUnitStatsAsset.BaseAttackSpeed => BaseAttackSpeed;
  FP64 IUnitStatsAsset.CritChance => CritChance;
  FP64 IUnitStatsAsset.CritDamage => CritDamage;
  FP64 IUnitStatsAsset.MoveSpeed => MoveSpeed;
  FP64 IUnitStatsAsset.AttackRange => AttackRange;
  FP64 IUnitStatsAsset.AcquisitionRange => AcquisitionRange;
  FP64 IUnitStatsAsset.AttackWindup => AttackWindup;
  FP64 IUnitStatsAsset.GameplayRadius => GameplayRadius;
  FP64 IUnitStatsAsset.PathingRadius => PathingRadius;

  // Returns zero for stats without per-level growth.
  public FP64 GrowthOf(StatType stat) => stat switch {
    StatType.MaxHealth => HealthPerLevel,
    StatType.MaxMana => ManaPerLevel,
    StatType.HealthRegen => HealthRegenPerLevel,
    StatType.ManaRegen => ManaRegenPerLevel,
    StatType.Armor => ArmorPerLevel,
    StatType.MagicResist => MagicResistPerLevel,
    StatType.AttackDamage => AttackDamagePerLevel,
    StatType.BonusAttackSpeed => BonusAttackSpeedPerLevel,
    _ => FP64.Zero
  };

  // Maps action slots to skill assets.
  public int GetSkillAssetId(int slot) {
    return slot switch {
      0 => Skill1AssetId,
      1 => Skill2AssetId,
      2 => Skill3AssetId,
      3 => Skill4AssetId,
      _ => 0
    };
  }
}
