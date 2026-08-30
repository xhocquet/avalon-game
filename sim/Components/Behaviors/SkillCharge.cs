using System.Runtime.InteropServices;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Components;

// A burst charging on its caster: armed by a cast, paid out on DetonateTick as one disc centred on
// wherever the caster is standing then. The counterpart to AttackProc - that one waits on an
// attack to spend it, this one waits only on the clock, so a charge always goes off unless the caster
// dies first.
//
// The payload is copied off the asset row at cast time rather than re-read at detonation, so the
// numbers are the ones the rank that armed it authored. One slot: re-arming replaces.
//
// A charge can also carry a channel aura: while it winds up, the disc is re-collected every payout
// interval and whoever stands in it now takes the whole damage accrued since the last pulse. That is
// transient membership - a unit that walked in is caught, one that left is not - which is what
// separates it from the lingering per-target burn DamageOverTime leaves behind. AuraAccrualPerTick 0
// means the charge is a plain burst with no aura.
[KlothoComponent(ComponentIds.SkillCharge)]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public partial struct SkillCharge : IComponent {
  public int SourceId; // SkillAsset id that armed it; 0 means nothing is charging
  public int DetonateTick;
  public int SnareDurationTicks; // How long the burst holds what it catches; 0 damages only
  public int AuraIntervalTicks; // Ticks between aura pulses
  public int AuraNextPulseTick; // Absolute tick the next aura pulse is due
  public FP64 Damage;
  public FP64 Radius;
  public FP64 AuraAccrualPerTick; // Per-second aura rate scaled to the tick length, fixed at arm time
  public FP64 AuraPending; // Aura damage accrued since the last pulse

  public readonly bool IsCharging => SourceId != 0;
  public readonly bool HasAura => AuraAccrualPerTick > FP64.Zero;

  public readonly bool IsDue(int tick) {
    return IsCharging && tick >= DetonateTick;
  }

  public void Clear() {
    SourceId = 0;
    DetonateTick = 0;
    SnareDurationTicks = 0;
    AuraIntervalTicks = 0;
    AuraNextPulseTick = 0;
    Damage = FP64.Zero;
    Radius = FP64.Zero;
    AuraAccrualPerTick = FP64.Zero;
    AuraPending = FP64.Zero;
  }
}
