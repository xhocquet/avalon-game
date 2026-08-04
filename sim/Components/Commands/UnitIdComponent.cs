using System.Runtime.InteropServices;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Components;

// Stable IDs to pass around commands and such
[KlothoComponent(ComponentIds.Unit)]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public partial struct UnitIdComponent : IComponent {
  public int UnitId;
  public int UnitTypeId;
}
