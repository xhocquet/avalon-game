using System.Runtime.InteropServices;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Models {
  [KlothoComponent(105)]
  [StructLayout(LayoutKind.Sequential, Pack = 4)]
  public partial struct Minion : IComponent {
    public int WaveId;
  }
}
