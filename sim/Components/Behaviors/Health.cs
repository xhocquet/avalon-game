using System.Runtime.InteropServices;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Components;

// Represents the minion's current HP, to be reduced/increased in game
[KlothoComponent(ComponentIds.Health)]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public partial struct Health(int current) : IComponent {
  public int Current = current;

  // UnitIdComponent.UnitId of whoever last reduced Current
  public int LastDamagerUnitId = 0;
}
