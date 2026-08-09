using System.Runtime.InteropServices;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Components;

// Lives in frame state rather than on a system so it rolls back deterministically - a counter kept
// outside the frame would keep advancing through a rollback and hand out ids the replayed ticks
// never used. Shared by both statically map-authored pickups (SimulationSetup.SpawnPickups) and ones
// an Oasis spawns at runtime, so ids never collide.
[KlothoComponent(ComponentIds.PickupIdCounter)]
[KlothoSingletonComponent]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public partial struct PickupIdCounter : IComponent, IIdCounter {
  public int NextPickupId;

  public int NextId {
    readonly get => NextPickupId;
    set => NextPickupId = value;
  }
}
