using xpTURN.Klotho.Deterministic.Math;
using System.Runtime.InteropServices;
using Meesles.Avalon.Sim.Assets;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Models;

[KlothoComponent(108)]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public partial struct Combat(MinionStatsAsset stats) : IComponent {
  public FP64 AttackRange = stats.AttackRange;
  public int AttackCooldownTicks = stats.AttackCooldownTicks;
  public int CooldownRemainingTicks = 0;
  public EntityRef Target;
}
