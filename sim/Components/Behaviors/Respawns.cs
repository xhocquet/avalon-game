using System.Runtime.InteropServices;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Components;

// Tag marking a unit that comes back instead of being destroyed on death. RespawnSystem owns these;
// DeathSystem excludes them. Says nothing about who — or what — is driving the unit.
[KlothoComponent(ComponentIds.Respawns)]
[StructLayout(LayoutKind.Sequential, Pack = 4, Size = 1)]
public partial struct Respawns : IComponent { }
