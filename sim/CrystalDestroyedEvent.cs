using xpTURN.Klotho.Core;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim {
  [KlothoSerializable(106)]
  public partial class CrystalDestroyedEvent : SimulationEvent {
    public override EventMode Mode => EventMode.Synced;

    [KlothoOrder(0)] public int UnitId;
    [KlothoOrder(1)] public int CrystalId;
    [KlothoOrder(2)] public int TeamId;
    [KlothoOrder(3)] public int OwnerId;
    [KlothoOrder(4)] public int DestroyerUnitId;
    [KlothoOrder(5)] public int DestroyerTeamId;
    [KlothoOrder(6)] public int DestroyerOwnerId;
  }
}
