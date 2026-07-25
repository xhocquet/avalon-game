using System.Runtime.InteropServices;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Models;

[KlothoComponent(108)]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
// The ability to attack: who this unit is targeting, how far it can reach, and how fast it can
// swing. The damage magnitude is NOT here - that's a Stats value (Stats.Strength), resolved at
// attack time by StatsSystem.CalculateDamage.
public partial struct Combat : IComponent {
  public FP64 AttackRange;
  public int AttackCooldownTicks;
  public int CooldownRemainingTicks;
  public EntityRef Target;
}
