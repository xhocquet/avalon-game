using System.Runtime.InteropServices;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Components;

// Added, counted down and removed by RespawnSystem. DeathSystem skips Respawns entities entirely.
[KlothoComponent(ComponentIds.PendingRespawn)]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public partial struct PendingRespawn : IComponent {
  public int RemainingTicks;
}
