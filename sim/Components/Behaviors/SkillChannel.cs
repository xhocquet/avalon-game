using System.Runtime.InteropServices;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Components;

// A self-channel that completes only if its caster remains at its starting planar position.
[KlothoComponent(ComponentIds.SkillChannel)]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public partial struct SkillChannel : IComponent {
  public int SourceId;
  public int CompleteTick;
  public int Rank;
  public int CancelsOnMove;
  public FPVector3 StartPosition;

  public readonly bool IsActive => SourceId != 0 && Rank > 0;
  public readonly bool IsDue(int tick) => IsActive && tick >= CompleteTick;

  public readonly bool HasMoved(FPVector3 position) {
    return CancelsOnMove != 0 &&
           (position.x != StartPosition.x || position.z != StartPosition.z);
  }

  public void Clear() {
    SourceId = 0;
    CompleteTick = 0;
    Rank = 0;
    CancelsOnMove = 0;
    StartPosition = FPVector3.Zero;
  }
}
