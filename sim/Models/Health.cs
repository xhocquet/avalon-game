using System.Runtime.InteropServices;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Models;

[KlothoComponent(103)]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public partial struct Health(int max) : IComponent {
  public int Current = max;
  public int Max = max;
}
