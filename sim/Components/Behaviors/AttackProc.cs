using System.Runtime.InteropServices;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Components;

// Multiplies the next landed auto-attack's damage, then clears.
// Re-arming replaces the pending proc. Expiry clears it without an attack.
// Cleared in place; the component stays attached.
[KlothoComponent(ComponentIds.AttackProc)]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public partial struct AttackProc : IComponent {
  public int SourceId; // Source skill asset id; 0 when inactive
  public int StartedTick;
  public int ExpiryTick;
  public FP64 DamageMultiplier; // Total damage multiplier; 4 means 400%

  public readonly bool IsArmed => SourceId != 0;
  public readonly EffectPolarity Polarity => IsArmed ? EffectPolarity.Buff : EffectPolarity.Neutral;

  public readonly bool IsExpired(int tick) {
    return IsArmed && tick >= ExpiryTick;
  }

  public void Clear() {
    SourceId = 0;
    StartedTick = 0;
    ExpiryTick = 0;
    DamageMultiplier = FP64.Zero;
  }
}
