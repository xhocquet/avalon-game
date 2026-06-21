using System.Runtime.InteropServices;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Models {
  [KlothoComponent(104)]
  [StructLayout(LayoutKind.Sequential, Pack = 4)]
  public partial struct Hero : IComponent {
    public int PlayerId;
    public int Level;
    public int Experience;
  }
}
