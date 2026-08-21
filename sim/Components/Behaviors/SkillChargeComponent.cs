using System.Runtime.InteropServices;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Components;

// A burst charging on its caster: armed by a cast, paid out on DetonateTick as one disc centred on
// wherever the caster is standing then. The counterpart to AttackProcComponent - that one waits on an
// attack to spend it, this one waits only on the clock, so a charge always goes off unless the caster
// dies first.
//
// The payload is copied off the asset row at cast time rather than re-read at detonation, so the
// numbers are the ones the rank that armed it authored. One slot: re-arming replaces.
[KlothoComponent(ComponentIds.SkillCharge)]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public partial struct SkillChargeComponent : IComponent {
  public int SourceId; // SkillAsset id that armed it; 0 means nothing is charging
  public int DetonateTick;
  public int SnareDurationTicks; // How long the burst holds what it catches; 0 damages only
  public FP64 Damage;
  public FP64 Radius;

  public readonly bool IsCharging => SourceId != 0;

  public readonly bool IsDue(int tick) {
    return IsCharging && tick >= DetonateTick;
  }

  public void Clear() {
    SourceId = 0;
    DetonateTick = 0;
    SnareDurationTicks = 0;
    Damage = FP64.Zero;
    Radius = FP64.Zero;
  }
}
