using System.Runtime.InteropServices;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Components;

[KlothoComponent(ComponentIds.Player)]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public partial struct Player : IComponent {
  public int PlayerId;
  public int Score;
}
