using System.Runtime.InteropServices;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Models;

[KlothoComponent(118)]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public partial struct Inventory : IComponent {
  public int Gold;
  public int GoldAccrualRemainderMs;
}
