using System.Runtime.InteropServices;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Components;

// Per-hero progression ledger, the XP counterpart to InventoryComponent's wallet. Kill sites deposit
// into Experience (see ExperienceRewards); ExperienceSystem is the only writer of Level, converting
// the running total into levels and the stat gains each one grants.
// Experience is lifetime XP earned, never spent and never reset by death, so the level thresholds in
// XpRulesAsset are cumulative rather than per-level.
[KlothoComponent(ComponentIds.Experience)]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public partial struct ExperienceComponent() : IComponent {
  public int Level = 1;
  public int Experience = 0;
}
