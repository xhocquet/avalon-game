using System.Runtime.InteropServices;
using Meesles.Avalon.Sim.Assets;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Components;

// Attack reach and cadence plus the currently acquired target. TargetAcquisitionSystem picks
// targets, AttackIntentSystem/DamageSystem resolve them.
[KlothoComponent(ComponentIds.Combat)]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public partial struct Combat(MinionStatsAsset stats) : IComponent {
  public FP64 AttackRange = stats.AttackRange;
  public int AttackCooldownTicks = stats.AttackCooldownTicks;
  public int CooldownRemainingTicks = 0;
  public EntityRef Target;
}
