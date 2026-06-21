using System.Runtime.InteropServices;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Models {
  [KlothoComponent(110)]
  [StructLayout(LayoutKind.Sequential, Pack = 4)]
  public partial struct UnitMoveTarget : IComponent {
    public FPVector3 Target;
  }
}
