using System.Runtime.InteropServices;
using Meesles.Avalon.Sim.Assets;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Components;

// Represents an in-progress attack
[KlothoComponent(ComponentIds.Combat)]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public partial struct Combat : IComponent {
  public FP64 AttackRange;
  public FP64 AttackReacquireRangeMultiplier;
  public int AttackCooldownTicks;
  public int CooldownRemainingTicks;

  // AttackIntentSystem's in-range resolution of AttackTargetUnitId; 0 when nothing is engaged.
  // A unit id rather than an EntityRef: entity slots recycle, unit ids never do.
  public int TargetUnitId;

  public static Combat From(IUnitStatsAsset stats) => new() {
    AttackRange = stats.AttackRange,
    AttackReacquireRangeMultiplier = stats.AttackReacquireRangeMultiplier,
    AttackCooldownTicks = stats.AttackCooldownTicks
  };
}
