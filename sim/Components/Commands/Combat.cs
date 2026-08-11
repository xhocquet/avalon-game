using System.Runtime.InteropServices;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Components;

// An in-progress attack, and nothing else. Range, acquisition range, and the base attack period all
// live on StatsComponent now, because items and skills change them and this component is transient
// per-attack state.
[KlothoComponent(ComponentIds.Combat)]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public partial struct Combat : IComponent {
  public int CooldownRemainingTicks;

  // AttackIntentSystem's in-range resolution of AttackTargetUnitId; 0 when nothing is engaged.
  // A unit id rather than an EntityRef: entity slots recycle, unit ids never do.
  public int TargetUnitId;
}
