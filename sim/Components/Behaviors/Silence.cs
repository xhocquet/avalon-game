using System.Runtime.InteropServices;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Components;

// A unit silenced: it still moves, turns, and auto-attacks, but cannot cast a skill until ExpiryTick.
// The hold is not a stat - it is a single gate in SkillActions.EvaluateCast, so the client greys the
// skill bar the same tick the sim would reject the cast.
//
// A single slot rather than a buffer, the same shape as Snare: overlapping silences keep the later
// expiry and the source that owns it, so a second one can extend the hold but never cut it short.
// SourceId is 0 when the unit is free, so the component stays on once added.
[KlothoComponent(ComponentIds.Silence)]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public partial struct Silence : IComponent {
  public int SourceId; // SkillAsset id silencing the unit; 0 means it is free
  public int ExpiryTick;

  public readonly bool IsSilenced => SourceId != 0;

  public readonly bool IsExpired(int tick) {
    return IsSilenced && tick >= ExpiryTick;
  }

  public void Clear() {
    SourceId = 0;
    ExpiryTick = 0;
  }
}
