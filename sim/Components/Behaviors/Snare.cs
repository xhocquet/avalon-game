using System.Runtime.InteropServices;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Components;

// Blocks movement without changing MoveSpeed. Turning, attacks, and casts remain available.
// Overlapping snares keep the later expiry and its source. Cleared in place.
[KlothoComponent(ComponentIds.Snare)]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public partial struct Snare : IComponent {
  public int SourceId; // Source skill asset id; 0 when inactive
  public int StartedTick;
  public int ExpiryTick;

  public readonly bool IsSnared => SourceId != 0;
  public readonly EffectPolarity Polarity => IsSnared ? EffectPolarity.Debuff : EffectPolarity.Neutral;

  public readonly bool IsExpired(int tick) {
    return IsSnared && tick >= ExpiryTick;
  }

  public void Clear() {
    SourceId = 0;
    StartedTick = 0;
    ExpiryTick = 0;
  }
}
