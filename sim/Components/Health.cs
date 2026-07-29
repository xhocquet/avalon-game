using System.Runtime.InteropServices;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Components;

[KlothoComponent(ComponentIds.Health)]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public partial struct Health(int max) : IComponent {
  public int Current = max;
  public int Max = max;

  // Unit.UnitId of whoever last reduced Current, so DeathSystem can credit the killing blow.
  // A UnitId rather than an EntityRef: ids come from a monotonic counter and never alias a
  // recycled entity slot.
  public int LastDamagerUnitId = 0;
}
