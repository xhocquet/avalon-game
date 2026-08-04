using System.Runtime.InteropServices;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Components;

[KlothoComponent(ComponentIds.Crystal)]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public partial struct Crystal : IComponent {
  public int CrystalId;
}
