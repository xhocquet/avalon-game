using System.Runtime.InteropServices;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Components;

[KlothoComponent(ComponentIds.Turret)]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public partial struct Turret : IComponent {
  public int TurretId;
}
