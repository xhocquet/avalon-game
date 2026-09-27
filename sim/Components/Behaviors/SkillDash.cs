using System.Runtime.InteropServices;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Components;

[KlothoComponent(ComponentIds.SkillDash)]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public partial struct SkillDash : IComponent {
  public FPVector3 StartPosition;
  public FPVector3 Destination;
  public FP64 Speed;
  public FP64 HealAmount;
  public int SourceId;
  public int Rank;

  public readonly bool IsActive => SourceId != 0 && Speed > FP64.Zero;

  public void Clear() {
    StartPosition = FPVector3.Zero;
    Destination = FPVector3.Zero;
    Speed = FP64.Zero;
    HealAmount = FP64.Zero;
    SourceId = 0;
    Rank = 0;
  }
}
