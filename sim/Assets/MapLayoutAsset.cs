using System.Collections.Generic;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon {
  [KlothoDataAsset(102, AssetId = 102, Key = "MapLayout")]
  public partial class MapLayoutAsset : IDataAsset {
    [KlothoOrder(0)] public int[] MarkerTypes;
    [KlothoOrder(1)] public int[] MarkerTeams;
    [KlothoOrder(2)] public FPVector3[] MarkerPositions;

    public List<(int team, FPVector3 position)> GetMarkersByType(MapMarkerType type) {
      var result = new List<(int, FPVector3)>();
      if (MarkerTypes == null) return result;
      int typeInt = (int)type;
      for (int i = 0; i < MarkerTypes.Length; i++)
        if (MarkerTypes[i] == typeInt)
          result.Add((MarkerTeams[i], MarkerPositions[i]));
      return result;
    }

    public bool TryGetByTypeAndTeam(MapMarkerType type, int teamId, out FPVector3 position) {
      position = FPVector3.Zero;
      if (MarkerTypes == null) return false;
      int typeInt = (int)type;
      for (int i = 0; i < MarkerTypes.Length; i++) {
        if (MarkerTypes[i] == typeInt && MarkerTeams[i] == teamId) {
          position = MarkerPositions[i];
          return true;
        }
      }
      return false;
    }
  }
}
