using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim;

// An auto-attack has begun its swing. The damage lands AttackWindup seconds later as the
// AttackHitEvent carrying the same AttackHitId, or never, if AttackWindupCanceledEvent lands first.
// This is the phase attack animations play on - starting them on the hit runs the wind-up after the
// damage it is supposed to lead.
[KlothoSerializable(120)]
public partial class AttackWindupStartedEvent : SimulationEvent {
  [KlothoOrder(0)] public int AttackHitId; // FK to AttackHitEvent.AttackHitId
  [KlothoOrder(1)] public int AttackerUnitId;
  [KlothoOrder(2)] public int TargetUnitId;
  [KlothoOrder(3)] public FPVector3 AttackerPosition;
  [KlothoOrder(4)] public FPVector3 TargetPosition;
  // Seconds, not ticks: the view has no business knowing the sim's tick rate, and the server
  // and a local host do not run at the same one.
  [KlothoOrder(5)] public FP64 WindupSeconds;
}
