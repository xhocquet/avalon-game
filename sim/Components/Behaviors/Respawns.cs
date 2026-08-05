using System.Runtime.InteropServices;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Components;

// Indicates a unit will respawn
[KlothoComponent(ComponentIds.Respawns)]
[StructLayout(LayoutKind.Sequential, Pack = 4, Size = 1)]
public partial struct Respawns : IComponent { }
