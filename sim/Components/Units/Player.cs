using System.Runtime.InteropServices;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Components;

// Score for a human player in game. NOT the human/hero component
[KlothoComponent(ComponentIds.Player)]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public partial struct Player : IComponent {
  public int Score;
}
