using System.Runtime.InteropServices;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Models;

// Singleton match-setup bookkeeping. TeamlessPruned flips to 1 once TeamPruneSystem has removed the
// bases/defenses/spawns of teams no player is on. Stored in frame state (not on the system) so it
// rolls back deterministically and the one-shot prune re-runs correctly after a rollback.
[KlothoComponent(124)]
[KlothoSingletonComponent]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public partial struct MatchSetupState : IComponent {
  public int TeamlessPruned;
}
