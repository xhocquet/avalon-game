using System.Runtime.InteropServices;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Components;

// Tag marking an entity a player may drive.
[KlothoComponent(ComponentIds.Controllable)]
[StructLayout(LayoutKind.Sequential, Pack = 4, Size = 1)]
public partial struct Controllable : IComponent { }
