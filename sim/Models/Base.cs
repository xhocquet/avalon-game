using System.Runtime.InteropServices;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Models {
  [KlothoComponent(106)]
  [StructLayout(LayoutKind.Sequential, Pack = 4)]
  public partial struct Base : IComponent {
    public int BaseId;
  }
}
