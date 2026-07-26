using System.Runtime.InteropServices;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Components;

// Who or what an entity is: the stable simulation id, its allegiance, and whether a player can
// drive it. Entity-kind tags (Hero/Minion/Turret/...) live in UnitTypes.cs.

[KlothoComponent(ComponentIds.Player)]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public partial struct Player : IComponent {
  public int PlayerId;
  public int Score;
}

[KlothoComponent(ComponentIds.Unit)]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public partial struct Unit : IComponent {
  public int UnitId;
  public int UnitTypeId;
}

[KlothoComponent(ComponentIds.Team)]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public partial struct Team(int teamId) : IComponent {
  public int TeamId = teamId;
}

[KlothoComponent(ComponentIds.Faction)]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public partial struct Faction(int factionId) : IComponent {
  public int FactionId = factionId;
}

[KlothoComponent(ComponentIds.Controllable)]
[StructLayout(LayoutKind.Sequential, Pack = 4, Size = 1)]
public partial struct Controllable : IComponent { }
