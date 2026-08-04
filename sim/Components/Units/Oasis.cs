using System.Runtime.InteropServices;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Components;

// Neutral resource fountain ejecting Pickups on a cooldown (see OasisSpawnSystem).
[KlothoComponent(ComponentIds.Oasis)]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public partial struct Oasis : IComponent {
  public int OasisId;
  public int SpawnCooldownRemainingMs;
}
