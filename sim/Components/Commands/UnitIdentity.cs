using System.Runtime.InteropServices;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Components;

// Stable identity carried on every unit: the id commands and events reference, plus the unit-type id.
// Resolved back to entities through UnitLookup.
[KlothoComponent(ComponentIds.UnitIdentity)]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public partial struct UnitIdentity : IComponent {
  public int UnitId;
  public int UnitTypeId;
}
