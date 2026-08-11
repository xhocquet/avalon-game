using System.Runtime.InteropServices;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Components;

// Current HP, moved only through DamageApplication and HealthApplication. FP64 rather than an int
// because the pool it clamps against (Stats.MaxHealth) is, and rounding here would eat every
// fractional buff before it reached the health bar.
[KlothoComponent(ComponentIds.Health)]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public partial struct Health(FP64 current) : IComponent {
  public FP64 Current = current;

  // UnitIdComponent.UnitId of whoever last reduced Current
  public int LastDamagerUnitId = 0;

  // A hero sits at zero while it waits on a respawn rather than being destroyed, so "not alive" and
  // "gone" are different states. Everything that filters corpses out asks through here.
  public readonly bool IsAlive => Current > FP64.Zero;
}
