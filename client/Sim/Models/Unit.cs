using System.Runtime.InteropServices;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon {
  [KlothoComponent(101)]
  [StructLayout(LayoutKind.Sequential, Pack = 4)]
  public partial struct Unit : IComponent {
    public int UnitId;
    public int UnitTypeId;
  }
}
