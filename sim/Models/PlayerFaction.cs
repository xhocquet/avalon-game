using System.Runtime.InteropServices;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Models {
  // Records a player's faction pick before their hero exists. Created/updated by
  // CommandSystem when a SelectFactionCommand is applied; read by HeroSpawnSystem to
  // stamp the Faction component (and pick the archetype) at hero spawn time.
  [KlothoComponent(116)]
  [StructLayout(LayoutKind.Sequential, Pack = 4)]
  public partial struct PlayerFaction : IComponent {
    public int PlayerId;
    public int TeamId;
    public int FactionId;
    // 1 once the player's SelectFactionCommand has been applied. HeroSpawnSystem spawns as soon
    // as this flips (or after a grace window, using the seeded default, if it never does).
    public int Confirmed;
  }
}
