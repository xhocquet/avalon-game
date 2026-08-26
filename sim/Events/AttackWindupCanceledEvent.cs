using xpTURN.Klotho.Core;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim;

// A swing that started will never land: the target died, left range, or stopped being hostile before
// the windup ran out. The attacker keeps the cooldown it paid at the swing, so a whiffed attack costs
// a full attack period rather than being free to re-roll.
[KlothoSerializable(121)]
public partial class AttackWindupCanceledEvent : SimulationEvent {
  [KlothoOrder(0)] public int AttackHitId; // The AttackWindupStartedEvent this cancels
  [KlothoOrder(1)] public int AttackerUnitId;
  [KlothoOrder(2)] public int TargetUnitId;
}
