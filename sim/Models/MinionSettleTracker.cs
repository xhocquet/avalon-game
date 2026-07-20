using System.Runtime.InteropServices;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Models;

// Per-minion progress tracker used by NavigationAgentSystem to decide when a minion should give
// up chasing its formation slot and settle in place. After a long group move the slot a minion
// was assigned at command time can end up unreachable across the packed blob; without this the
// minion charges the crowd forever (frozen or oscillating) and the group never stops shuffling.
// Added lazily when a minion is steering toward a slot and removed once it settles.
[KlothoComponent(125)]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public partial struct MinionSettleTracker : IComponent {
  // The slot this tracker is measuring against; if the minion is retargeted the tracker resets.
  public FP64 TargetX;
  public FP64 TargetZ;

  // Closest distance to the slot reached since the last meaningful improvement, and how many
  // consecutive ticks we've failed to beat it by SettleProgressStep. StuckTicks past a threshold
  // (while near the slot) means settle. Linear distance so the "made progress" test is speed-
  // agnostic and catches frozen, oscillating, and slowly-creeping minions alike.
  public FP64 BestDist;
  public int StuckTicks;
}
