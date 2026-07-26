using System.Runtime.InteropServices;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Components;

// World-wide singletons. These live in frame state rather than on a system so they roll back
// deterministically - a counter kept outside the frame would keep advancing through a rollback and
// hand out ids the replayed ticks never used.

[KlothoComponent(ComponentIds.UnitIdCounter)]
[KlothoSingletonComponent]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public partial struct UnitIdCounter : IComponent {
  public int NextUnitId;
}

[KlothoComponent(ComponentIds.PickupIdCounter)]
[KlothoSingletonComponent]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public partial struct PickupIdCounter : IComponent {
  public int NextPickupId;
}

// Singleton match-setup bookkeeping. TeamlessPruned flips to 1 once TeamPruneSystem has removed the
// bases/defenses/spawns of teams no player is on. Stored in frame state (not on the system) so it
// rolls back deterministically and the one-shot prune re-runs correctly after a rollback.
[KlothoComponent(ComponentIds.MatchSetupState)]
[KlothoSingletonComponent]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public partial struct MatchSetupState : IComponent {
  public int TeamlessPruned;
}
