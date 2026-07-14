using System.Runtime.InteropServices;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Models;

// Transient: added to an Oasis entity once its resource has been ejected and is in flight.
// Removed by OasisSpawnSystem once RemainingMs elapses, at which point the Pickup entity is
// actually created at TargetPosition.
[KlothoComponent(122)]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public partial struct OasisResourceLanding : IComponent {
  public int PickupId;
  public int Amount;
  public FPVector3 TargetPosition;
  public int RemainingMs;
}
