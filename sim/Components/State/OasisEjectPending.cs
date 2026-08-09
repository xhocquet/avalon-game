using System.Runtime.InteropServices;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Components;

// Temporary indicator of an oasis about to spawn a resource
[KlothoComponent(ComponentIds.OasisEjectPending)]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public partial struct OasisEjectPending : IComponent {
  public int PickupId;
  public int Amount;
  public int TypeAssetId;
  public FPVector3 TargetPosition;
  public int RemainingMs;
}
