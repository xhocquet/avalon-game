using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim.Assets;

// Instance id is AssetIds.MapLayout; look it up with TryGet<MapLayoutAsset>()
[KlothoDataAsset(AssetIds.TypeIds.MapLayout, AssetId = AssetIds.MapLayout, Key = "MapLayout")]
public partial class MapLayoutAsset : IDataAsset {
  [KlothoOrder(0)] public int[] MarkerTypes;
  [KlothoOrder(1)] public int[] MarkerTeams;
  [KlothoOrder(2)] public FPVector3[] MarkerPositions;
  [KlothoOrder(3)] public int[] MarkerValues;

  public bool TryGetByTypeAndTeam(MapMarkerType type, int teamId, out FPVector3 position) {
    position = FPVector3.Zero;
    if (MarkerTypes == null) return false;
    var typeInt = (int)type;
    for (var i = 0; i < MarkerTypes.Length; i++)
      if (MarkerTypes[i] == typeInt && MarkerTeams[i] == teamId) {
        position = MarkerPositions[i];
        return true;
      }

    return false;
  }
}
