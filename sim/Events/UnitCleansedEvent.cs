using xpTURN.Klotho.Core;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim;

[KlothoSerializable(SimulationEventTypeIds.UnitCleansed)]
public partial class UnitCleansedEvent : SimulationEvent {
  [KlothoOrder(0)] public int UnitId;

  public override EventMode Mode => EventMode.Synced;
}
