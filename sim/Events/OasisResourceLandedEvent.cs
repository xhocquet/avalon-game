using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim;

// Fired the tick the Pickup entity is actually created at Position. The entity itself renders via
// the normal view factory; this event is for a landing-impact cue (dust puff, sound, etc).
[KlothoSerializable(111)]
public partial class OasisResourceLandedEvent : SimulationEvent {
  [KlothoOrder(0)] public int PickupId;
  [KlothoOrder(1)] public FPVector3 Position;
  [KlothoOrder(2)] public int Amount;
  public override EventMode Mode => EventMode.Synced;
}
