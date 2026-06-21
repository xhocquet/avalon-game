using System.Runtime.InteropServices;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon {
  [KlothoComponent(109)]
  [KlothoSingletonComponent]
  [StructLayout(LayoutKind.Sequential, Pack = 4)]
  public partial struct UnitIdCounter : IComponent {
    public int NextUnitId;
  }
}
