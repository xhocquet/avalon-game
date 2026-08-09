using System.Runtime.InteropServices;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Components;

// A human player's match record, carried on their hero entity. NOT the human/hero component - it holds
// only what the scoreboard and the end-of-match result read back. Every field is written through
// MatchStats so a kill counts the same whether it landed via an auto-attack or a skill.
[KlothoComponent(ComponentIds.Player)]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public partial struct Player : IComponent {
  public int Score;
  public int HeroKills;
  public int Deaths;
  public int MinionKills;
  public int StructureKills; // turrets and crystals
  public int DamageDealt; // post-mitigation, hostiles only
}
