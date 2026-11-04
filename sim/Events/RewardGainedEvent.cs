using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim;

[KlothoSerializable(SimulationEventTypeIds.RewardGained)]
public partial class RewardGainedEvent : SimulationEvent {
  public override EventMode Mode => EventMode.Synced;
  [KlothoOrder(0)] public int TeamId;
  [KlothoOrder(1)] public int Gold;
  [KlothoOrder(2)] public int Xp;
  [KlothoOrder(3)] public int ResourceTypeId;
  [KlothoOrder(4)] public int ResourceAmount;
  [KlothoOrder(5)] public FPVector3 Position;
}
