using System.Runtime.InteropServices;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Components;

// Each landed attack decrements Remaining and sets the next cooldown from DelayTicks.
// Cleared in place; the component stays attached.
[KlothoComponent(ComponentIds.AttackBurst)]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public partial struct AttackBurst : IComponent {
  public int SourceId; // Source skill asset id; 0 when inactive
  public int StartedTick;
  public int Remaining; // Total burst attack count minus 1 when queued
  public int DelayTicks; // Requested cooldown; the normal cooldown is used if shorter
  public int ExpiryTick;

  public readonly bool IsQueued => SourceId != 0 && Remaining > 0;
  public readonly EffectPolarity Polarity => IsQueued ? EffectPolarity.Buff : EffectPolarity.Neutral;

  public readonly bool IsExpired(int tick) {
    return SourceId != 0 && tick >= ExpiryTick;
  }

  public void Clear() {
    SourceId = 0;
    StartedTick = 0;
    Remaining = 0;
    DelayTicks = 0;
    ExpiryTick = 0;
  }
}
