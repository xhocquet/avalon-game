using System;
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

  // Identity, not a display label: the exporter fills it from the map scene's filename, so it is
  // whatever the .tscn is called. A pretty name for the UI belongs in a client-side catalog keyed
  // off this, the way FactionCatalog carries the names FactionAsset doesn't.
  [KlothoOrder(4)] public string MapName;

  // The exporter fills the three required arrays in lockstep, but a hand-edited MapLayout.json can
  // leave them ragged. Scanning to the shortest drops the trailing markers on every peer identically
  // instead of throwing partway through a lockstep tick. MarkerValues is excluded: it postdates the
  // others, so layouts authored before it are legitimately short, and its readers already clamp.
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
