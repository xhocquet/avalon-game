using System.Runtime.InteropServices;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Components;

// Record of player faction selection, written during match setup by SelectFactionCommand.
[KlothoComponent(ComponentIds.PlayerFaction)]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public partial struct PlayerFaction : IComponent {
  public int PlayerId;
  public int TeamId;
  public int FactionId;
  public int Confirmed; // 0 = not confirmed, 1 = confirmed
}
