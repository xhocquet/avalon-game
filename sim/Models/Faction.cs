using System.Runtime.InteropServices;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Models;

// Synced runtime state: which faction a spawned unit belongs to. Attached at spawn so the
// view layer resolves the correct PackedScene immediately (the view factory resolves once at
// creation and does not re-resolve when a component is added later). FactionId == the
// FactionAsset's AssetId.
[KlothoComponent(115)]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public partial struct Faction : IComponent {
  public int FactionId;
}
