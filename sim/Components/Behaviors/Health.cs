using System.Runtime.InteropServices;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Components;

// A unit's consumable pools: the values that deplete and refill against a Stats max, as opposed to
// the buffable maxes themselves (Stats.MaxHealth, Stats.MaxMana) which live on Stats. Health
// was the first; mana joined it, and further spendable pools (energy, a shield) land here the same
// way rather than as new components.
//
// Each pool is FP64, not an int, because the Stats max it clamps against is - rounding here would eat
// every fractional buff before it reached the bar. Health moves only through DamageApplication and
// HealthApplication; mana only through ManaApplication.
[KlothoComponent(ComponentIds.Health)]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public partial struct Health(FP64 current) : IComponent {
  public FP64 Current = current; // Current HP, clamped against Stats.MaxHealth

  // Current mana, clamped against Stats.MaxMana. Spent by skill casts and refilled by ManaRestore
  // effects. A unit with no mana pool (minions, structures) sits at zero and every spend fails.
  public FP64 Mana = FP64.Zero;

  // UnitIdentity.UnitId of whoever last reduced Current
  public int LastDamagerUnitId = 0;

  // A hero sits at zero while it waits on a respawn rather than being destroyed, so "not alive" and
  // "gone" are different states. Everything that filters corpses out asks through here.
  public readonly bool IsAlive => Current > FP64.Zero;
}
