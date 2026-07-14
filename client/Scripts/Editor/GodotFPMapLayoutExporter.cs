// Collects SimMarkerNode instances from the edited scene and serializes
// their world-space positions into MapLayout.bytes + JSON sidecar.
// plugin.gd instantiates this [GlobalClass] and calls ExportMapLayout().

using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Godot;
using Meesles.Avalon.Sim.Assets;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.ECS.Json;

namespace Meesles.Avalon;

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
    var values = new List<int>();

    CollectMarkers(root, types, teams, positions, values);

    if (types.Count == 0)
      GD.PushWarning("[GodotFPMapLayoutExporter] No SimMarkerNode instances found in scene.");

    var asset = new MapLayoutAsset {
      MarkerTypes = types.ToArray(),
      MarkerTeams = teams.ToArray(),
      MarkerPositions = positions.ToArray(),
      MarkerValues = values.ToArray()
    };

    Save(asset);
  }

  private static readonly Regex TeamFolderPattern = new(@"^Team(\d+)$", RegexOptions.Compiled);

  private static void CollectMarkers(
    Node node,
    List<int> types, List<int> teams, List<FPVector3> positions, List<int> values) {
    if (node is Client.Scripts.SimMarkerNode marker) {
      types.Add((int)marker.MarkerType);
      teams.Add(ResolveTeam(marker));
      positions.Add(marker.GlobalTransform.Origin.ToFPVector3());
      values.Add(marker.Value);
    }

    foreach (var child in node.GetChildren())
      CollectMarkers(child, types, teams, positions, values);
  }

  // SimMarkerNode.Team is only reliable when the marker is itself the root of its instanced
  // scene (e.g. Spawn.tscn, Shop.tscn) — property overrides written in the parent scene target
  // the instance root, so a Team override set on a marker nested inside a prefab (e.g. Crystal.tscn,
  // Turret.tscn, whose SimMarkerNode lives on a child "SimMarker" node) silently binds to nothing
  // and the marker keeps Team=0. Deriving the team from the nearest ancestor named "TeamN" (the
  // World.tscn convention already used to group each team's markers) sidesteps that footgun
  // entirely — no per-marker Team property to forget. Falls back to the export field for markers
  // that aren't organized under a TeamN folder (e.g. neutral markers like Oasis).
  private static int ResolveTeam(Client.Scripts.SimMarkerNode marker) {
    for (var ancestor = marker.GetParent(); ancestor != null; ancestor = ancestor.GetParent()) {
      var match = TeamFolderPattern.Match(ancestor.Name);
      if (match.Success)
        return int.Parse(match.Groups[1].Value);
    }

    return marker.Team;
  }

  private static void Save(MapLayoutAsset asset) {
    var serializables = new List<IDataAssetSerializable> { asset };
    var bytes = DataAssetWriter.SerializeMixedCollectionToBytes(serializables);

    var absBytes = ProjectSettings.GlobalizePath(OutputBytesPath);
    File.WriteAllBytes(absBytes, bytes);

    var absJson = ProjectSettings.GlobalizePath(OutputJsonPath);
    var json = DataAssetJsonSerializer.SerializeMixedCollection(new List<IDataAsset> { asset });
    File.WriteAllText(absJson, json);

    EditorInterface.Singleton.GetResourceFilesystem().Scan();
    GD.Print($"[GodotFPMapLayoutExporter] Exported {asset.MarkerTypes.Length} markers → {OutputBytesPath}");
  }
}
