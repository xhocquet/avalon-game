using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim;

public static class DamageOverTimeController {
  public const int PayoutIntervalMs = 1000;

  public static bool ApplySkill(ref Frame frame, EntityRef target, int sourceUnitId, SkillAsset skill, int rank) =>
    rank > 0 && ApplyFromUnitId(ref frame, target, sourceUnitId, skill.AssetId, skill.DotDamagePerSecondAtRank(rank),
      TickMath.MsToTicksCeil(ref frame, skill.DotDurationMs), skill.DotPolicy);

  public static bool Apply(ref Frame frame, EntityRef target, EntityRef source, int sourceSkillAssetId,
    FP64 damagePerSecond, int durationTicks, BuffApplicationPolicy? applicationPolicy = null) =>
    ApplyFromUnitId(ref frame, target, UnitLookup.GetUnitId(ref frame, source), sourceSkillAssetId,
      damagePerSecond, durationTicks, applicationPolicy ?? BuffApplicationPolicy.Default);

  private static bool ApplyFromUnitId(ref Frame frame, EntityRef target, int sourceUnitId, int sourceSkillAssetId,
    FP64 damagePerSecond, int durationTicks, BuffApplicationPolicy policy) {
    var targetUnitId = UnitLookup.GetUnitId(ref frame, target);
    if (sourceSkillAssetId == 0 || targetUnitId == 0 || durationTicks <= 0 || damagePerSecond <= FP64.Zero ||
        durationTicks > int.MaxValue - frame.Tick || !policy.IsValid || sourceUnitId < 0 ||
        (policy.Identity == BuffIdentity.SkillPerCaster && sourceUnitId == 0) ||
        !frame.Has<Health>(target) || !frame.GetReadOnly<Health>(target).IsAlive)
      return false;

    var matches = FindMatches(ref frame, targetUnitId, sourceSkillAssetId, sourceUnitId, policy.Identity);
    if (!TrySelectEffect(matches, in policy, out var selection))
      return false;

    var refresh = selection.Effect.IsValid && !selection.Replace;
    var dot = refresh ? frame.GetReadOnly<DamageOverTime>(selection.Effect) : CreateState(ref frame);
    if (!TrySetLifetime(ref dot, frame.Tick, durationTicks, refresh ? policy.Duration : BuffDurationPolicy.Restart))
      return false;

    dot.TargetUnitId = targetUnitId;
    dot.SourceSkillAssetId = sourceSkillAssetId;
    dot.SourceUnitId = sourceUnitId;
    dot.AccrualPerTick = damagePerSecond * FP64.FromInt(TickMath.DeltaTimeMs(ref frame)) / FP64.FromInt(1000);
    if (refresh)
      frame.Get<DamageOverTime>(selection.Effect) = dot;
    else {
      var effect = frame.CreateEntity();
      frame.Add(effect, dot);
      if (selection.Replace)
        frame.Get<DamageOverTime>(selection.Effect).ExpiryTick = frame.Tick; // Final payout on the timer pass
    }

    return true;
  }

  public static void Tick(ref Frame frame, EntityRef effect, UnitLookup.Index units) {
    var dot = frame.GetReadOnly<DamageOverTime>(effect);
    if (!dot.IsBurning || !units.TryGet(dot.TargetUnitId, out var target) ||
        !frame.Has<Health>(target) || !frame.GetReadOnly<Health>(target).IsAlive) {
      frame.DestroyEntity(effect);
      return;
    }

    var expired = dot.IsExpired(frame.Tick);
    if (!expired)
      dot.Pending += dot.AccrualPerTick;

    var payout = FP64.Zero;
    if (expired || frame.Tick >= dot.NextPayoutTick) {
      var whole = FP64.Floor(dot.Pending);
      if (whole >= FP64.One) {
        dot.Pending -= whole;
        payout = whole;
      }

      dot.NextPayoutTick += dot.IntervalTicks;
    }

    if (expired)
      frame.DestroyEntity(effect);
    else
      frame.Get<DamageOverTime>(effect) = dot;

    if (payout <= FP64.Zero)
      return;

    var source = units.TryGet(dot.SourceUnitId, out var s) ? s : default;
    DamageController.ApplyDamage(ref frame, source, target, payout, DamageType.Magical);
  }

  public static void Clear(ref Frame frame, EntityRef target) {
    var targetUnitId = UnitLookup.GetUnitId(ref frame, target);
    if (targetUnitId == 0)
      return;
    var filter = frame.Filter<DamageOverTime>();
    while (filter.Next(out var effect)) {
      ref var dot = ref frame.Get<DamageOverTime>(effect);
      if (dot.TargetUnitId == targetUnitId)
        dot.Clear(); // Reaped by TimedEffectSystem on its next pass
    }
  }

  public static bool IsBurning(ref Frame frame, EntityRef target) {
    var targetUnitId = UnitLookup.GetUnitId(ref frame, target);
    if (targetUnitId == 0)
      return false;
    var filter = frame.Filter<DamageOverTime>();
    while (filter.Next(out var effect)) {
      ref readonly var dot = ref frame.GetReadOnly<DamageOverTime>(effect);
      if (dot.TargetUnitId == targetUnitId && dot.IsBurning && !dot.IsExpired(frame.Tick))
        return true;
    }

    return false;
  }

  private readonly record struct DotMatches(int Count, EntityRef Oldest, EntityRef Weakest);

  private readonly record struct DotSelection(EntityRef Effect, bool Replace);

  private static DotMatches FindMatches(ref Frame frame, int targetUnitId, int sourceSkillAssetId,
    int sourceUnitId, BuffIdentity identity) {
    var count = 0;
    var oldest = default(EntityRef);
    var weakest = default(EntityRef);
    var oldestState = default(DamageOverTime);
    var weakestState = default(DamageOverTime);
    var filter = frame.Filter<DamageOverTime>();
    while (filter.Next(out var effect)) {
      ref readonly var dot = ref frame.GetReadOnly<DamageOverTime>(effect);
      if (!dot.IsBurning || dot.IsExpired(frame.Tick) || dot.TargetUnitId != targetUnitId ||
          dot.SourceSkillAssetId != sourceSkillAssetId ||
          (identity == BuffIdentity.SkillPerCaster && dot.SourceUnitId != sourceUnitId))
        continue;
      count++;
      if (!oldest.IsValid || IsOlder(in dot, effect, in oldestState, oldest)) {
        oldest = effect;
        oldestState = dot;
      }

      if (!weakest.IsValid || dot.AccrualPerTick < weakestState.AccrualPerTick ||
          (dot.AccrualPerTick == weakestState.AccrualPerTick && IsOlder(in dot, effect, in weakestState, weakest))) {
        weakest = effect;
        weakestState = dot;
      }
    }

    return new DotMatches(count, oldest, weakest);
  }

  private static bool IsOlder(in DamageOverTime candidate, EntityRef candidateEntity,
    in DamageOverTime current, EntityRef currentEntity) =>
    candidate.StartedTick < current.StartedTick ||
    (candidate.StartedTick == current.StartedTick && candidateEntity.ToId() < currentEntity.ToId());

  private static bool TrySelectEffect(DotMatches matches, in BuffApplicationPolicy policy,
    out DotSelection selection) {
    selection = default;
    if (matches.Count == 0 ||
        (policy.Reapplication == BuffReapplication.Stack && matches.Count < policy.MaxStacks))
      return true;
    if (policy.Reapplication == BuffReapplication.Ignore)
      return false;
    if (policy.Reapplication == BuffReapplication.Refresh || policy.Overflow == BuffStackOverflow.RefreshOldest) {
      selection = new DotSelection(matches.Oldest, false);
      return true;
    }

    if (policy.Overflow == BuffStackOverflow.ReplaceWeakest) {
      selection = new DotSelection(matches.Weakest, true);
      return true;
    }

    return false;
  }

  private static DamageOverTime CreateState(ref Frame frame) {
    var interval = TickMath.MsToTicksCeil(ref frame, PayoutIntervalMs);
    if (interval < 1)
      interval = 1;
    return new DamageOverTime {
      IntervalTicks = interval,
      NextPayoutTick = frame.Tick + interval
    };
  }

  private static bool TrySetLifetime(ref DamageOverTime dot, int tick, int duration, BuffDurationPolicy policy) {
    var expiry = tick + duration;
    if (policy == BuffDurationPolicy.KeepLaterExpiry && dot.ExpiryTick >= expiry)
      return true;
    if (policy == BuffDurationPolicy.Extend) {
      if (dot.ExpiryTick > int.MaxValue - duration)
        return false;
      dot.ExpiryTick += duration;
      return true;
    }

    dot.StartedTick = tick;
    dot.ExpiryTick = expiry;
    return true;
  }
}
