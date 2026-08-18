using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim.Assets;

// Instance ids live in the AssetIds.Skill* block; look one up with Get<SkillAsset>(id)
[KlothoDataAsset(AssetIds.TypeIds.Skill)]
public partial class SkillAsset : IDataAsset {
  [KlothoOrder(0)] public int MaxRank;
  [KlothoOrder(1)] public int CooldownMs;
  [KlothoOrder(2)] public FP64 Damage;
  [KlothoOrder(3)] public FP64 DamagePerRank;
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

  // Timed stat buff block. BuffPercent is the fraction of the stat added at rank 1 and
  // BuffPercentPerRank the step each rank after; which stats it lands on is the skill's own business.
  [KlothoOrder(12)] public int BuffDurationMs;
  [KlothoOrder(13)] public FP64 BuffPercent;
  [KlothoOrder(14)] public FP64 BuffPercentPerRank;

  // Empowered-attack block. ProcDamageMultiplier is the total multiplier the armed attack lands with
  // (4 is 400% of a normal hit) and ProcDurationMs how long the charge waits to be spent.
  [KlothoOrder(15)] public int ProcDurationMs;
  [KlothoOrder(16)] public FP64 ProcDamageMultiplier;
  [KlothoOrder(17)] public FP64 ProcDamageMultiplierPerRank;

  public bool HasCastRange => MinCastRange > FP64.Zero || MaxCastRange > FP64.Zero;

  public FP64 DamageAtRank(int rank) {
    return rank <= 0 ? FP64.Zero : Damage + DamagePerRank * FP64.FromInt(rank - 1);
  }

  public FP64 BuffPercentAtRank(int rank) {
    return rank <= 0 ? FP64.Zero : BuffPercent + BuffPercentPerRank * FP64.FromInt(rank - 1);
  }

  public FP64 ProcDamageMultiplierAtRank(int rank) {
    return rank <= 0
      ? FP64.Zero
      : ProcDamageMultiplier + ProcDamageMultiplierPerRank * FP64.FromInt(rank - 1);
  }
}
