using xpTURN.Klotho.Core;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim;

[KlothoSerializable(107)]
public partial class TurretDestroyedEvent : SimulationEvent {
  [KlothoOrder(1)] public int DestroyerUnitId;

  [KlothoOrder(0)] public int UnitId;
  public override EventMode Mode => EventMode.Synced;
}
