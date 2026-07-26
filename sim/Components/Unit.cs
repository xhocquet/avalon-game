using System.Runtime.InteropServices;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Components;

// The stable simulation id every gameplay unit is addressed by. Commands reference UnitId rather
// than transient ECS entity ids (see sim/AGENTS.md).
[KlothoComponent(ComponentIds.Unit)]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public partial struct Unit : IComponent {
  public int UnitId;
  public int UnitTypeId;
}
