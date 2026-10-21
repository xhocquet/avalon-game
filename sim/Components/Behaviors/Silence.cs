using System.Runtime.InteropServices;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Components;

// Blocks skill casts until ExpiryTick. Movement, turning, and auto-attacks are unaffected.
// Overlapping silences keep the later expiry and its source. Cleared in place.
[KlothoComponent(ComponentIds.Silence)]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public partial struct Silence : IComponent {
  public int SourceId; // Source skill asset id; 0 when inactive
  public int StartedTick;
  public int ExpiryTick;

  public readonly bool IsSilenced => SourceId != 0;
  public readonly EffectPolarity Polarity => IsSilenced ? EffectPolarity.Debuff : EffectPolarity.Neutral;

  public readonly bool IsExpired(int tick) {
    return IsSilenced && tick >= ExpiryTick;
  }

  public void Clear() {
    SourceId = 0;
    StartedTick = 0;
    ExpiryTick = 0;
  }
}
