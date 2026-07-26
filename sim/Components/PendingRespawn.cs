using System.Runtime.InteropServices;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Components;

// Added by DeathSystem, counted down and cleared by RespawnSystem.
[KlothoComponent(ComponentIds.PendingRespawn)]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public partial struct PendingRespawn : IComponent {
  public int RemainingTicks;
}
