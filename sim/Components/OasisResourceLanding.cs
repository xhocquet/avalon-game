using System.Runtime.InteropServices;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Components;

// Temporary indicator of a resource flying through the air from an oasis
[KlothoComponent(ComponentIds.OasisResourceLanding)]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public partial struct OasisResourceLanding : IComponent {
  public int PickupId;
  public int Amount;
  public FPVector3 TargetPosition;
  public int RemainingMs;
}
