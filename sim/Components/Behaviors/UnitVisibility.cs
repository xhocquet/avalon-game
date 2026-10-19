using System.Runtime.InteropServices;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Components;

[KlothoComponent(ComponentIds.UnitVisibility)]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public partial struct UnitVisibility : IComponent {
  public ulong Teams; // Team ids 1–64 map to bits 0–63.
}
