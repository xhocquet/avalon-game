using System.Runtime.InteropServices;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Components;

// The human's match record. Rides on the hero entity today; the identity of the human driving that
// hero lives on Hero.PlayerId, not here.
[KlothoComponent(ComponentIds.Player)]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public partial struct Player : IComponent {
  public int Score;
}
