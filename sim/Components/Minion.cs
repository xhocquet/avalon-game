using System.Runtime.InteropServices;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Components;

[KlothoComponent(ComponentIds.Minion)]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public partial struct Minion : IComponent {
  public int WaveId;
}
