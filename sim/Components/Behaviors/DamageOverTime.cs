using System.Runtime.InteropServices;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Components;

// One effect entity per DOT stack. Accrual and payout clocks belong to this instance.
[KlothoComponent(ComponentIds.DamageOverTime)]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public partial struct DamageOverTime : IComponent {
  public int SourceSkillAssetId; // 0 marks a cleared effect
  public int SourceUnitId; // Latest caster; receives kill credit
  public int TargetUnitId;
  public int StartedTick;
  public int ExpiryTick;
  public int IntervalTicks; // ticks between payouts
  public int NextPayoutTick; // absolute tick the next payout is due
  public FP64 AccrualPerTick; // Refreshed rate affects future accrual only
  public FP64 Pending; // damage accrued since the last payout

  public readonly bool IsBurning => SourceSkillAssetId != 0;
  public readonly EffectPolarity Polarity => IsBurning ? EffectPolarity.Debuff : EffectPolarity.Neutral;

  public readonly bool IsExpired(int tick) {
    return IsBurning && tick >= ExpiryTick;
  }

  public void Clear() {
    SourceSkillAssetId = 0;
    SourceUnitId = 0;
    TargetUnitId = 0;
    StartedTick = 0;
    ExpiryTick = 0;
    IntervalTicks = 0;
    NextPayoutTick = 0;
    AccrualPerTick = FP64.Zero;
    Pending = FP64.Zero;
  }
}
