using System.Runtime.InteropServices;
using Meesles.Avalon.Sim.Assets;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Components;

// Damage, targeting, and the death/respawn cycle. TargetAcquisitionSystem picks targets,
// AttackIntentSystem/DamageSystem resolve them, DeathSystem/RespawnSystem handle the aftermath.

[KlothoComponent(ComponentIds.Health)]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public partial struct Health(int max) : IComponent {
  public int Current = max;
  public int Max = max;
}

[KlothoComponent(ComponentIds.Combat)]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public partial struct Combat(MinionStatsAsset stats) : IComponent {
  public FP64 AttackRange = stats.AttackRange;
  public int AttackCooldownTicks = stats.AttackCooldownTicks;
  public int CooldownRemainingTicks = 0;
  public EntityRef Target;
}

[KlothoComponent(ComponentIds.AttackTargetUnitId)]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public partial struct AttackTargetUnitId : IComponent {
  public int TargetUnitId;
}

[KlothoComponent(ComponentIds.PendingRespawn)]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public partial struct PendingRespawn : IComponent {
  public int RemainingTicks;
}
