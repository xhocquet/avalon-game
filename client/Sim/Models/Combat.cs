using System.Runtime.InteropServices;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon {
  [KlothoComponent(108)]
  [StructLayout(LayoutKind.Sequential, Pack = 4)]
  public partial struct Combat : IComponent {
    public int AttackDamage;
    public FP64 AttackRange;
    public int AttackCooldownTicks;
    public int CooldownRemainingTicks;
    public EntityRef Target;
  }
}
