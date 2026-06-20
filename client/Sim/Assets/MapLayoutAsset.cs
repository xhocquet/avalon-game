using System.Collections.Generic;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon {
  [KlothoDataAsset(102, AssetId = 102, Key = "MapLayout")]
  public partial class MapLayoutAsset : IDataAsset {
    [KlothoOrder(0)] public int[] MarkerIds;
    [KlothoOrder(1)] public int[] MarkerTypes;
    [KlothoOrder(2)] public int[] MarkerTeams;
    [KlothoOrder(3)] public FPVector3[] MarkerPositions;

    public FPVector3 GetPosition(int markerId) {
      if (MarkerIds == null) return FPVector3.Zero;
      for (int i = 0; i < MarkerIds.Length; i++)
        if (MarkerIds[i] == markerId) return MarkerPositions[i];
      return FPVector3.Zero;
    }

    public List<(int markerId, int team, FPVector3 position)> GetMarkersByType(MapMarkerType type) {
      var result = new List<(int, int, FPVector3)>();
      if (MarkerTypes == null) return result;
      int typeInt = (int)type;
      for (int i = 0; i < MarkerTypes.Length; i++)
        if (MarkerTypes[i] == typeInt)
          result.Add((MarkerIds[i], MarkerTeams[i], MarkerPositions[i]));
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
