using System.Runtime.InteropServices;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Components;

// Entity-kind tags. Each carries the per-kind id systems key off; the shared identity/allegiance
// components live in Identity.cs.

[KlothoComponent(ComponentIds.Hero)]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public partial struct Hero(int playerId) : IComponent {
  public int PlayerId = playerId;
  public int Level = 1;
  public int Experience = 0;
}

[KlothoComponent(ComponentIds.Minion)]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public partial struct Minion : IComponent {
  public int WaveId;
}

[KlothoComponent(ComponentIds.Turret)]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public partial struct Turret : IComponent {
  public int TurretId;
}

[KlothoComponent(ComponentIds.Crystal)]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public partial struct Crystal : IComponent {
  public int CrystalId;
}

[KlothoComponent(ComponentIds.SpawnPoint)]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public partial struct SpawnPoint : IComponent {
  public int SpawnPointId;
  public int UnitTypeId;
}
