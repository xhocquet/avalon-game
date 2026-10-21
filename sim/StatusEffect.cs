using xpTURN.Klotho.Deterministic.Math;

namespace Meesles.Avalon.Sim;

public enum StatusEffectKind {
  Buff = 0,
  DamageOverTime = 1,
  Snare = 2,
  Silence = 3,
  AttackProc = 4,
  AttackBurst = 5
}

// Buff slots identify slot occupants; DOT ids include the effect entity's generation.
public readonly record struct StatusEffectKey(int TargetUnitId, StatusEffectKind Kind, long InstanceId);

public readonly record struct StatusEffectTiming(int? StartedTick, int ExpiryTick) {
  public int? DurationTicks => StartedTick.HasValue
    ? ExpiryTick > StartedTick.Value ? ExpiryTick - StartedTick.Value : 0
    : null;

  public int GetRemainingTicks(int tick) {
    var remaining = ExpiryTick > tick ? ExpiryTick - tick : 0;
    var duration = DurationTicks;
    return duration.HasValue && remaining > duration.Value ? duration.Value : remaining;
  }

  public FP64? GetRemainingFraction(int tick) {
    var duration = DurationTicks;
    if (!duration.HasValue)
      return null;
    return duration.Value > 0
      ? FP64.FromInt(GetRemainingTicks(tick)) / FP64.FromInt(duration.Value)
      : FP64.Zero;
  }
}

public readonly record struct StatusEffectChange(BuffChange Change, EffectPolarity Polarity);

public readonly record struct StatusEffectDot(
  FP64 AccrualPerTick, FP64 Pending, int IntervalTicks, int NextPayoutTick, int TickIntervalMs, DamageType DamageType) {
  public FP64 DamagePerSecond => TickIntervalMs > 0
    ? AccrualPerTick * FP64.FromInt(1000) / FP64.FromInt(TickIntervalMs)
    : FP64.Zero;
}

public readonly record struct StatusEffectBurst(int RemainingExtraAttacks, int DelayTicks);

// A value snapshot; no references to mutable frame state.
public readonly record struct StatusEffect {
  public StatusEffectKey Key { get; internal init; }
  public StatusEffectKind Kind => Key.Kind;
  public int TargetUnitId => Key.TargetUnitId;
  public int SourceSkillAssetId { get; internal init; }
  public int SourceUnitId { get; internal init; } // 0 when attribution is not stored
  public int? Rank { get; internal init; }
  public EffectPolarity Polarity { get; internal init; }
  public StatusEffectTiming Timing { get; internal init; }
  public StatusEffectChanges Changes { get; internal init; }
  public StatusEffectDot? Dot { get; internal init; }
  public FP64? AttackDamageMultiplier { get; internal init; }
  public StatusEffectBurst? Burst { get; internal init; }
}
