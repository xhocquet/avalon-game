using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim;

[KlothoSerializable(SimulationEventTypeIds.CrystalDestroyed)]
public partial class CrystalDestroyedEvent : SimulationEvent {
  [KlothoOrder(0)] public int UnitId;
  [KlothoOrder(1)] public int CrystalId;
  [KlothoOrder(2)] public int TeamId;
  [KlothoOrder(3)] public int DestroyerUnitId;
  [KlothoOrder(4)] public int DestroyerTeamId;
  [KlothoOrder(5)] public FPVector3 Position;
  [KlothoOrder(6)] public int DestroyerPlayerId;
  [KlothoOrder(7)] public int DestroyerFactionId;
  [KlothoOrder(8)] public int PlayerId;
  [KlothoOrder(9)] public int FactionId;

  public override EventMode Mode => EventMode.Synced;
}
