using System;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim.Assets;

[KlothoDataAsset(AssetIds.TypeIds.MapLayout, AssetId = AssetIds.MapLayout, Key = "MapLayout")]
public partial class MapLayoutAsset : IDataAsset {
  [KlothoOrder(0)] public int[] MarkerTypes;
  [KlothoOrder(1)] public int[] MarkerTeams;
  [KlothoOrder(2)] public FPVector3[] MarkerPositions;
  [KlothoOrder(3)] public int[] MarkerValues;

  [KlothoOrder(4)] public string MapName; // Source scene filename, not a display name

  [KlothoOrder(5)] public FPVector2 VisionOrigin;
  [KlothoOrder(6)] public FPVector2 VisionSize;
  [KlothoOrder(7)] public FP64 VisionCellSize;
  [KlothoOrder(8)] public FPVector2[] SightEdges;

  private SightGeometry _sightGeometry;
  public SightGeometry SightGeometry => _sightGeometry ??= new SightGeometry(SightEdges, VisionCellSize);

  // Hand-edited layouts can have ragged required arrays; ignore the trailing entries consistently.
  public int MarkerCount =>
    MarkerTypes == null || MarkerTeams == null || MarkerPositions == null
      ? 0
      : Math.Min(MarkerTypes.Length, Math.Min(MarkerTeams.Length, MarkerPositions.Length));

  public bool TryGetByTypeAndTeam(MapMarkerType type, int teamId, out FPVector3 position) {
    position = FPVector3.Zero;
    var typeInt = (int)type;
    var markerCount = MarkerCount;
    for (var i = 0; i < markerCount; i++)
      if (MarkerTypes[i] == typeInt && MarkerTeams[i] == teamId) {
        position = MarkerPositions[i];
        return true;
      }

    return false;
  }
}
