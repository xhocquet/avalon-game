using System.Runtime.InteropServices;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Models {
  [KlothoComponent(111)]
  [StructLayout(LayoutKind.Sequential, Pack = 4)]
  public partial struct AttackTargetUnitId : IComponent {
    public int TargetUnitId;
  }
}
