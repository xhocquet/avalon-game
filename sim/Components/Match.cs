using System.Runtime.InteropServices;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Components;

// Per-player match state that isn't tied to a spawned unit: the faction pick made during setup and
// the tunable stat block applied to a hero. Match-wide singletons live in Singletons.cs.

// Record of player faction selection
[KlothoComponent(ComponentIds.PlayerFaction)]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public partial struct PlayerFaction : IComponent {
  public int PlayerId;
  public int TeamId;
  public int FactionId;
  public int Confirmed; // 0 = not confirmed, 1 = confirmed
}

[KlothoComponent(ComponentIds.Stats)]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public partial struct Stats() : IComponent {
  public int Strength = 100;
  public int Defense = 100;
  public int Speed = 100;
}
