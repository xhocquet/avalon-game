using System.Runtime.InteropServices;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Models;

[KlothoComponent(113)]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public partial struct Turret : IComponent {
  public int TurretId;
}
