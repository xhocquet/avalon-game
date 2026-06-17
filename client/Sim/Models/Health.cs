using System.Runtime.InteropServices;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon {
  [KlothoComponent(103)]
  [StructLayout(LayoutKind.Sequential, Pack = 4)]
  public partial struct Health : IComponent {
    public int Current;
    public int Max;
  }
}
