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

  // 0/1. Arming clears the caster's attack cooldown, so the empowered swing lands at once instead of
  // waiting out the auto-attack that came before it. Per-row because not every proc wants it.
  [KlothoOrder(18)] public int ProcResetsAttackCooldown;

  // Heal block, authored as a fraction of the target's MaxHealth rather than a flat number so it keeps
  // its meaning as the pool grows with level. HealPercent is rank 1, HealPercentPerRank the step after.
  [KlothoOrder(19)] public FP64 HealPercent;
  [KlothoOrder(20)] public FP64 HealPercentPerRank;

  // 0/1. The skill has no aim: it resolves on the caster and the aim point is replaced with the
  // caster's own position, so nothing downstream has to decide whether the target means anything. The
  // client reads the same flag to fire on key-down instead of holding the key to aim.
  [KlothoOrder(21)] public int SelfCast;

  // Attack-burst block. BurstAttackCount is how many auto-attacks the cast is worth in total (2 is a
  // double swing), BurstAttackDelayMs the spacing between them, and BurstDurationMs how long the
  // queued swings wait for a target before they lapse.
  [KlothoOrder(22)] public int BurstAttackCount;
  [KlothoOrder(23)] public int BurstAttackCountPerRank;
  [KlothoOrder(24)] public int BurstAttackDelayMs;
  [KlothoOrder(25)] public int BurstDurationMs;

  // 0/1. Queuing clears the caster's swing timer, so the burst opens on the cast tick instead of
  // waiting out the auto-attack that came before it. Same field as ProcResetsAttackCooldown, for the
  // other lifecycle.
  [KlothoOrder(26)] public int BurstResetsAttackCooldown;

  // Cone block. ConeRange is how far the wedge reaches from the caster's centre and ConeAngleDegrees
  // its full opening angle; what it does to what it catches is the skill's own business, and its
  // damage comes off the shared Damage/DamagePerRank pair.
  [KlothoOrder(27)] public FP64 ConeRange;
  [KlothoOrder(28)] public FP64 ConeAngleDegrees;

  public bool HasCastRange => MinCastRange > FP64.Zero || MaxCastRange > FP64.Zero;
  public bool IsSelfCast => SelfCast != 0;
  public bool HasCone => ConeRange > FP64.Zero && ConeAngleDegrees > FP64.Zero;

  public FP64 DamageAtRank(int rank) {
    return rank <= 0 ? FP64.Zero : Damage + DamagePerRank * FP64.FromInt(rank - 1);
  }

  public FP64 BuffPercentAtRank(int rank) {
    return rank <= 0 ? FP64.Zero : BuffPercent + BuffPercentPerRank * FP64.FromInt(rank - 1);
  }

  public FP64 HealPercentAtRank(int rank) {
    return rank <= 0 ? FP64.Zero : HealPercent + HealPercentPerRank * FP64.FromInt(rank - 1);
  }

  public int BurstAttackCountAtRank(int rank) {
    return rank <= 0 ? 0 : BurstAttackCount + BurstAttackCountPerRank * (rank - 1);
  }

  public FP64 ProcDamageMultiplierAtRank(int rank) {
    return rank <= 0
      ? FP64.Zero
      : ProcDamageMultiplier + ProcDamageMultiplierPerRank * FP64.FromInt(rank - 1);
  }
}
