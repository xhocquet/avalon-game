using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim.Assets;

// Instance ids live in the AssetIds.Skill* block; look one up with Get<SkillAsset>(id)
[KlothoDataAsset(AssetIds.TypeIds.Skill)]
public partial class SkillAsset : IDataAsset {
  [KlothoOrder(0)] public int MaxRank;
  [KlothoOrder(1)] public int CooldownMs;
  [KlothoOrder(2)] public int Damage;
  [KlothoOrder(3)] public int DamagePerRank;
  [KlothoOrder(4)] public FP64 ProjectileSpeed; // Units/second
  [KlothoOrder(5)] public FP64 ProjectileRange;
  [KlothoOrder(6)] public FP64 ProjectileRadius;
  [KlothoOrder(7)] public int ProjectileCount;
  [KlothoOrder(8)] public FP64 ProjectileSpacing;
  [KlothoOrder(9)] public FP64 ProjectileSpawnOffset; // Range is measured from the offset position

  // Cast band around the caster, both optional: omitted in JSON (0) means unbounded on that end.
  // SkillAim.ClampToCastRange pulls the aim point onto the band before the effect runs.
  [KlothoOrder(10)] public FP64 MinCastRange;
  [KlothoOrder(11)] public FP64 MaxCastRange;

  public bool HasCastRange => MinCastRange > FP64.Zero || MaxCastRange > FP64.Zero;

  public int DamageAtRank(int rank) {
    return rank <= 0 ? 0 : Damage + DamagePerRank * (rank - 1);
  }
}
