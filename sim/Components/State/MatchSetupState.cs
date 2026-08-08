using System.Runtime.InteropServices;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Components;

// Singleton match-setup bookkeeping. TeamlessPruned flips to 1 once TeamPruneSystem has removed the
// bases/defenses/spawns of teams no player is on. Stored in frame state (not on the system) so it
// rolls back deterministically and the one-shot prune re-runs correctly after a rollback.
[KlothoComponent(ComponentIds.MatchSetupState)]
[KlothoSingletonComponent]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public partial struct MatchSetupState : IComponent {
  public int TeamlessPruned;

  // Distinct teams left holding a crystal once the prune settled. ScoreSystem needs the starting
  // count to tell "one crystal left standing" from "this match only ever had one base".
  public int ContenderTeamCount;
}
