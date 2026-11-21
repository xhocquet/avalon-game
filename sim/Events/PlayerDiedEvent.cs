using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim;

[KlothoSerializable(SimulationEventTypeIds.PlayerDied)]
public partial class PlayerDiedEvent : SimulationEvent {
  [KlothoOrder(0)] public int PlayerId;
  [KlothoOrder(1)] public int TeamId;
  [KlothoOrder(2)] public int UnitId;
  [KlothoOrder(3)] public FPVector3 Position;
  [KlothoOrder(4)] public int RespawnDelayTicks;
  [KlothoOrder(5)] public int FactionId;
  [KlothoOrder(6)] public int DestroyerUnitId;
  [KlothoOrder(7)] public int DestroyerPlayerId;
  [KlothoOrder(8)] public int DestroyerTeamId;
  [KlothoOrder(9)] public int DestroyerFactionId;

  public override EventMode Mode => EventMode.Synced;
}
