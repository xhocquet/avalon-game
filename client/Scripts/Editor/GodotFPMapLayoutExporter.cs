// Collects SimMarkerNode instances from the edited scene and serializes
// their world-space positions into MapLayout.bytes + JSON sidecar.
// plugin.gd instantiates this [GlobalClass] and calls ExportMapLayout().

using System.Collections.Generic;
using global::Godot;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.ECS.Json;
using Meesles.Avalon.Sim.Assets;

namespace Meesles.Avalon {
  [Tool]
  [GlobalClass]
  public partial class GodotFPMapLayoutExporter : RefCounted {
    private const string OutputBytesPath = "res://Sim/Data/MapLayout.bytes";
    private const string OutputJsonPath = "res://Sim/Data/MapLayout.json";

    public void ExportMapLayout() {
      var root = EditorInterface.Singleton.GetEditedSceneRoot();
      if (root == null) {
        GD.PushError("[GodotFPMapLayoutExporter] No scene open.");
        return;
      }

      var types = new List<int>();
      var teams = new List<int>();
      var positions = new List<FPVector3>();

      CollectMarkers(root, types, teams, positions);

      if (types.Count == 0)
        GD.PushWarning("[GodotFPMapLayoutExporter] No SimMarkerNode instances found in scene.");

      var asset = new MapLayoutAsset {
        MarkerTypes = types.ToArray(),
        MarkerTeams = teams.ToArray(),
        MarkerPositions = positions.ToArray(),
      };

      Save(asset);
    }

    private static void CollectMarkers(
      Node node,
      List<int> types, List<int> teams, List<FPVector3> positions) {
      if (node is SimMarkerNode marker) {
        types.Add((int)marker.MarkerType);
        teams.Add(marker.Team);
        positions.Add(marker.GlobalTransform.Origin.ToFPVector3());
      }

      foreach (Node child in node.GetChildren())
        CollectMarkers(child, types, teams, positions);
    }

    private static void Save(MapLayoutAsset asset) {
      var serializables = new List<IDataAssetSerializable> { asset };
      byte[] bytes = DataAssetWriter.SerializeMixedCollectionToBytes(serializables);

      string absBytes = ProjectSettings.GlobalizePath(OutputBytesPath);
      System.IO.File.WriteAllBytes(absBytes, bytes);

      string absJson = ProjectSettings.GlobalizePath(OutputJsonPath);
      string json = DataAssetJsonSerializer.SerializeMixedCollection(new List<IDataAsset> { asset });
      System.IO.File.WriteAllText(absJson, json);

      EditorInterface.Singleton.GetResourceFilesystem().Scan();
      GD.Print($"[GodotFPMapLayoutExporter] Exported {asset.MarkerTypes.Length} markers → {OutputBytesPath}");
    }
  }
}
