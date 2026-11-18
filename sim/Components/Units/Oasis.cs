using System.Runtime.InteropServices;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Components;

// One live neutral fountain; spawned pickups are separate entities.
[KlothoComponent(ComponentIds.Oasis, MaxCount = SimulationCapacity.OasisSlots)]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public partial struct Oasis : IComponent {
  public int OasisId;
  public int PickupTypeAssetId; // What this oasis ejects; authored on the map marker
  public int SpawnCooldownRemainingMs;
}
