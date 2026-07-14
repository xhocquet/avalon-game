using System.Runtime.InteropServices;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Models;

[KlothoComponent(117)]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public partial struct Oasis : IComponent {
  public int OasisId;

  // Counts down to the next resource spawn; see OasisSpawnSystem.SpawnIntervalMs.
  public int SpawnCooldownRemainingMs;
}
