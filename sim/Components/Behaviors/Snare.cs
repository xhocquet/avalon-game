using System.Runtime.InteropServices;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Components;

// A unit rooted in place: it still turns, attacks, and casts, but nothing moves it until ExpiryTick.
// Both movement paths - NavigationAgentSystem and CommandSystem's direct integrator - check this
// before they steer, so a snared agent is frozen rather than merely slowed to zero.
//
// A single slot rather than a buffer: overlapping snares keep the later expiry and the source that
// owns it, so a second one can extend the hold but never cut it short. SourceId is 0 when the unit
// is free, so the component stays on once added.
[KlothoComponent(ComponentIds.Snare)]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public partial struct Snare : IComponent {
  public int SourceId; // SkillAsset id holding the unit; 0 means it is free
  public int ExpiryTick;

  public readonly bool IsSnared => SourceId != 0;

  public readonly bool IsExpired(int tick) {
    return IsSnared && tick >= ExpiryTick;
  }

  public void Clear() {
    SourceId = 0;
    ExpiryTick = 0;
  }
}
