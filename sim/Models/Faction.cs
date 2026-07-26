using System.Runtime.InteropServices;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Models;

[KlothoComponent(115)]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public partial struct Faction(int factionId) : IComponent {
  public int FactionId = factionId;
}
