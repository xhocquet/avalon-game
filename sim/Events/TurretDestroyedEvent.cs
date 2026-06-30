using xpTURN.Klotho.Core;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim {
  [KlothoSerializable(107)]
  public partial class TurretDestroyedEvent : SimulationEvent {
    public override EventMode Mode => EventMode.Synced;

    [KlothoOrder(0)] public int UnitId;
    [KlothoOrder(1)] public int DestroyerUnitId;
  }
}
