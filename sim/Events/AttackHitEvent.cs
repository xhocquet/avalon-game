using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim {
  [KlothoSerializable(108)]
  public partial class AttackHitEvent : SimulationEvent {
    [KlothoOrder(0)] public int AttackerUnitId;
    [KlothoOrder(1)] public int TargetUnitId;
    [KlothoOrder(2)] public int Damage;
    [KlothoOrder(3)] public FPVector3 AttackerPosition;
    [KlothoOrder(4)] public FPVector3 TargetPosition;
  }
}
