using System.Runtime.InteropServices;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Components;

// A magic-damage burn ticking on its target: attached by an effect that leaves it behind (a projectile
// hit today), paid out over the window rather than on contact. Like a snare or a stat buff the window
// is an absolute expiry tick, so TimedEffectSystem is one comparison per burning unit and a rollback
// replay ends it on the tick it first did.
//
// One slot: a second application refreshes, keeping the rate and expiry being applied now. Damage
// accrues every sim tick into Pending at AccrualPerTick but only reaches DamageApplication on an
// interval boundary (NextPayoutTick) and once more at expiry - the target takes a handful of solid
// hits over the window instead of a floored point every frame.
[KlothoComponent(ComponentIds.DamageOverTime)]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public partial struct DamageOverTimeComponent : IComponent {
  public int SourceId; // SkillAsset id burning the unit; 0 means it is not burning
  public int SourceUnitId; // who the kill credits
  public int ExpiryTick;
  public int IntervalTicks; // ticks between payouts
  public int NextPayoutTick; // absolute tick the next payout is due
  public FP64 AccrualPerTick; // per-second rate scaled to the tick length, fixed at attach time
  public FP64 Pending; // damage accrued since the last payout

  public readonly bool IsBurning => SourceId != 0;

  public readonly bool IsExpired(int tick) {
    return IsBurning && tick >= ExpiryTick;
  }

  public void Clear() {
    SourceId = 0;
    SourceUnitId = 0;
    ExpiryTick = 0;
    IntervalTicks = 0;
    NextPayoutTick = 0;
    AccrualPerTick = FP64.Zero;
    Pending = FP64.Zero;
  }
}
