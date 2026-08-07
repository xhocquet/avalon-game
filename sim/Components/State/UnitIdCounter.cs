using System.Runtime.InteropServices;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Components;

// Lives in frame state rather than on a system so it rolls back deterministically - a counter kept
// outside the frame would keep advancing through a rollback and hand out ids the replayed ticks
// never used.
[KlothoComponent(ComponentIds.UnitIdCounter)]
[KlothoSingletonComponent]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public partial struct UnitIdCounter : IComponent, IIdCounter {
  public int NextUnitId;

  public int NextId { readonly get => NextUnitId; set => NextUnitId = value; }
}
