using System.Runtime.InteropServices;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Components;

[KlothoComponent(ComponentIds.Team)]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public partial struct Team(int teamId) : IComponent {
  public int TeamId = teamId;
}
