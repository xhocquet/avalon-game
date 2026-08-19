using System.Runtime.InteropServices;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Components;

// Extra auto-attacks owed to the unit: a cast queues them, each attack that lands spends one and
// leaves DelayTicks on the swing timer instead of the full period, so the burst comes out at once.
// The sibling of AttackProcComponent - that one changes what an attack is worth, this one changes
// how soon the next one comes.
//
// Remaining counts swings *past* the one the cast bought, so a two-attack burst queues 1. The
// component stays on the unit once added; SourceId is 0 when nothing is queued.
[KlothoComponent(ComponentIds.AttackBurst)]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public partial struct AttackBurstComponent : IComponent {
  public int SourceId; // SkillAsset id that queued it; 0 means nothing is queued
  public int Remaining;
  public int DelayTicks; // Swing timer left after each attack the burst pays for
  public int ExpiryTick;

  public readonly bool IsQueued => SourceId != 0 && Remaining > 0;

  public readonly bool IsExpired(int tick) {
    return SourceId != 0 && tick >= ExpiryTick;
  }

  public void Clear() {
    SourceId = 0;
    Remaining = 0;
    DelayTicks = 0;
    ExpiryTick = 0;
  }
}
