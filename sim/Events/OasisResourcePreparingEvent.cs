using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim;

// Fired the instant an Oasis starts winding up a resource, before it's actually ejected.
// Lets the view telegraph the coming eject (e.g. a glow/charge-up at the oasis).
[KlothoSerializable(109)]
public partial class OasisResourcePreparingEvent : SimulationEvent {
  [KlothoOrder(0)] public int OasisId;
  [KlothoOrder(1)] public int PickupId;
  [KlothoOrder(2)] public FPVector3 OasisPosition;
  [KlothoOrder(3)] public FPVector3 TargetPosition;
  [KlothoOrder(4)] public int PrepareDurationMs;
  public override EventMode Mode => EventMode.Synced;
}
