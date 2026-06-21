using System.Runtime.InteropServices;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Models {
  [KlothoComponent(102)]
  [StructLayout(LayoutKind.Sequential, Pack = 4)]
  public partial struct Team : IComponent {
    public int TeamId;
  }
}
