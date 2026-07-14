using System.Runtime.InteropServices;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Models;

[KlothoComponent(123)]
[KlothoSingletonComponent]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public partial struct PickupIdCounter : IComponent {
  public int NextPickupId;
}
