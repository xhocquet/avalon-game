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

  // A hero sits at zero while it waits on a respawn rather than being destroyed, so "not alive" and
  // "gone" are different states. Everything that filters corpses out asks through here.
  public readonly bool IsAlive => Current > 0;
}
