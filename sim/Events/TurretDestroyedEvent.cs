using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim;

[KlothoSerializable(SimulationEventTypeIds.TurretDestroyed)]
public partial class TurretDestroyedEvent : SimulationEvent {
  [KlothoOrder(1)] public int DestroyerUnitId;
  [KlothoOrder(0)] public int UnitId;
  [KlothoOrder(2)] public FPVector3 Position;
  [KlothoOrder(3)] public int TeamId;
  [KlothoOrder(4)] public int DestroyerPlayerId;
  [KlothoOrder(5)] public int DestroyerTeamId;
  [KlothoOrder(6)] public int DestroyerFactionId;
  [KlothoOrder(7)] public int PlayerId;
  [KlothoOrder(8)] public int FactionId;

  public override EventMode Mode => EventMode.Synced;
}
