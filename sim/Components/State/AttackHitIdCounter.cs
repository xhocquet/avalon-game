using System.Runtime.InteropServices;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Components;

// Every landed hit gets an id so the events that describe it can point at it. A hit is one instant
// rather than a lifetime, so unlike ProjectileIdCounter nothing stores this on an entity - it exists
// only to correlate the events raised about one hit within a tick.
[KlothoComponent(ComponentIds.AttackHitIdCounter)]
[KlothoSingletonComponent]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public partial struct AttackHitIdCounter : IComponent, IIdCounter {
  public int NextAttackHitId;

  public int NextId {
    readonly get => NextAttackHitId;
    set => NextAttackHitId = value;
  }
}
