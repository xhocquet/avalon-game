using System.Runtime.InteropServices;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Components;

// An empowered auto-attack waiting to be spent: armed by a cast, consumed by the next attack that
// lands, dropped if neither happens before ExpiryTick. The counterpart to StatBuffsComponent - both
// expire on a tick, but a buff is worth something every tick it holds while this is worth nothing
// until the one attack that spends it.
//
// A single slot rather than a buffer: a second proc replaces the one waiting rather than queueing
// behind it, which is what a player expects from re-arming and keeps the consume path one branch.
// SourceId is 0 when nothing is armed, so the component stays on the unit once added.
[KlothoComponent(ComponentIds.AttackProc)]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public partial struct AttackProcComponent : IComponent {
  public int SourceId; // SkillAsset id that armed it; 0 means nothing is armed
  public int ExpiryTick;
  public FP64 DamageMultiplier; // Total multiplier on the hit: 4 is 400% of a normal attack

  public readonly bool IsArmed => SourceId != 0;

  public readonly bool IsExpired(int tick) {
    return IsArmed && tick >= ExpiryTick;
  }

  public void Clear() {
    SourceId = 0;
    ExpiryTick = 0;
    DamageMultiplier = FP64.Zero;
  }
}
