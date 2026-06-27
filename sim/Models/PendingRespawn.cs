using System.Runtime.InteropServices;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Models {
  [KlothoComponent(112)]
  [StructLayout(LayoutKind.Sequential, Pack = 4)]
  public partial struct PendingRespawn : IComponent {
    public int RemainingTicks;
  }
}
