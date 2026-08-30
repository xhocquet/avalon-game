using System.Runtime.InteropServices;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Components;

// An in-progress attack, and nothing else. Range, acquisition range, and the base attack period all
// live on Stats now, because items and skills change them and this component is transient
// per-attack state.
[KlothoComponent(ComponentIds.Combat)]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public partial struct Combat : IComponent {
  public int CooldownRemainingTicks;

  // AttackIntentSystem's in-range resolution of AttackTargetUnitId; 0 when nothing is engaged.
  // A unit id rather than an EntityRef: entity slots recycle, unit ids never do.
  public int TargetUnitId;

  // The tick the swing in progress lands its damage on; 0 when no swing is in progress. Absolute
  // rather than a countdown, the way buffs and procs hold theirs, because nothing pauses or refunds
  // a windup once it starts - it is set once and only ever compared against.
  public int WindupReleaseTick;

  // Allocated when the swing starts so AttackWindupStartedEvent, any proc that modifies the damage
  // on the way in, and the AttackHitEvent that lands all report the same hit.
  public int WindupAttackHitId;

  // The target the swing was aimed at, held separately from TargetUnitId: retargeting mid-swing
  // must not silently redirect damage that was already committed to someone else.
  public int WindupTargetUnitId;
}
