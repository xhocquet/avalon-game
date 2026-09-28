using System.Runtime.InteropServices;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Components;

// A unit's consumable hp and mp. Depletes and heals. Represented as FP64 to support % heals
[KlothoComponent(ComponentIds.Health)]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public partial struct Health(FP64 current) : IComponent {
  public FP64 Current = current; // current HP clamped against Stats.MaxHealth
  public FP64 Mana = FP64.Zero; // current mana clamped against Stats.MaxMana
  public int LastDamagerUnitId = 0; // UnitIdentity.UnitId of whoever last reduced Current

  public readonly bool IsAlive => Current > FP64.Zero;
}
