using System.Runtime.InteropServices;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Models;

// Transient: added to an Oasis entity while its telegraphed resource is winding up. Removed by
// OasisSpawnSystem once RemainingMs elapses, at which point the resource is actually ejected.
[KlothoComponent(121)]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public partial struct OasisEjectPending : IComponent {
  public int PickupId;
  public int Amount;
  public FPVector3 TargetPosition;
  public int RemainingMs;
}
