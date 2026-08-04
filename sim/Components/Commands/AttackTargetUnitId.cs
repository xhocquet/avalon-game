using System.Runtime.InteropServices;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Components;

[KlothoComponent(ComponentIds.AttackTargetUnitId)]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public partial struct AttackTargetUnitId : IComponent {
  public int TargetUnitId;
}
