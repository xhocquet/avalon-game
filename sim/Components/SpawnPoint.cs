using System.Runtime.InteropServices;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Components;

// Marker WaveSpawnSystem spawns minion waves from.
[KlothoComponent(ComponentIds.SpawnPoint)]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public partial struct SpawnPoint : IComponent {
  public int SpawnPointId;
  public int UnitTypeId;
}
