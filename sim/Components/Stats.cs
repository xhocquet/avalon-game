using System.Runtime.InteropServices;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Components;

// Tunable stat block applied to a hero.
[KlothoComponent(ComponentIds.Stats)]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public partial struct Stats() : IComponent {
  public int Strength = 100;
  public int Defense = 100;
  public int Speed = 100;
}
