using System.Runtime.InteropServices;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Components;

[KlothoComponent(ComponentIds.Hero)]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public partial struct Hero(int playerId, int heroStatsAssetId) : IComponent {
  public int PlayerId = playerId;
  public int HeroStatsAssetId = heroStatsAssetId; // Get<HeroStatsAsset>(id)
}
