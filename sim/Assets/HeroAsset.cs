using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim.Assets;

// Instance ids live in the AssetIds.Hero* block; look one up with Get<HeroAsset>(id).
[KlothoDataAsset(AssetIds.TypeIds.Hero)]
public partial class HeroAsset : IDataAsset {
  [KlothoOrder(0)] public int BehaviorId;
  [KlothoOrder(1)] public int Health;
  [KlothoOrder(2)] public FP64 MoveSpeed;
  [KlothoOrder(3)] public FP64 Radius;
  [KlothoOrder(4)] public int AttackDamage;
  [KlothoOrder(5)] public FP64 AttackRange;
  [KlothoOrder(6)] public int AttackCooldownTicks;
  [KlothoOrder(7)] public FP64 AttackReacquireRangeMultiplier;
  [KlothoOrder(8)] public int Defense;
  [KlothoOrder(9)] public int SkillSetId; // Maps to IHeroSkillSet
  [KlothoOrder(10)] public int Skill1AssetId; // Maps to SkillAsset
  [KlothoOrder(11)] public int Skill2AssetId; // Maps to SkillAsset
  [KlothoOrder(12)] public int Skill3AssetId; // Maps to SkillAsset
  [KlothoOrder(13)] public int Skill4AssetId; // Maps to SkillAsset

  // Maps action slots to skill ID positions
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
