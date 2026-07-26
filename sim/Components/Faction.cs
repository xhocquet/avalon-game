using System.Runtime.InteropServices;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Components;

[KlothoComponent(ComponentIds.Faction)]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public partial struct Faction(int factionId) : IComponent {
  public int FactionId = factionId;
}
