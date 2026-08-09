using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim;

// Fired when a pickup lands at it's final location
[KlothoSerializable(111)]
public partial class OasisResourceLandedEvent : SimulationEvent {
  [KlothoOrder(0)] public int PickupId;
  [KlothoOrder(1)] public FPVector3 Position;
  [KlothoOrder(2)] public int Amount;
  [KlothoOrder(3)] public int TypeAssetId;

  public override EventMode Mode => EventMode.Synced;
}
