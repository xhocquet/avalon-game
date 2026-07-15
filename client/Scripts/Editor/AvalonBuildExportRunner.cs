#if TOOLS
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Godot;
using Meesles.Avalon.Sim.Assets;
using xpTURN.Klotho.Deterministic.Geometry;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.Deterministic.Physics;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.ECS.Json;
using xpTURN.Klotho.Godot;

namespace Meesles.Avalon;

[Tool]
[GlobalClass]
public partial class AvalonBuildExportRunner : RefCounted {
  private const string DefaultScenePath = "res://Scenes/World/World.tscn";
  private const string MapLayoutBytesPath = "res://Sim/Data/MapLayout.bytes";
  private const string MapLayoutJsonPath = "res://Sim/Data/MapLayout.json";

  // Node under NavigationRegion3D whose descendants are hidden to bake the "walls down" navmesh
  // variant. MeshInstance3D visibility (not collision) is what Godot's default nav bake parses.
  private const string GatesNodeName = "Gates";
  private const string OpenNavMeshSuffix = "_Open";

  public bool Run(string scenePath = DefaultScenePath) {
    var packed = ResourceLoader.Load<PackedScene>(scenePath);
    if (packed == null) {
      GD.PushError($"[AvalonBuildExportRunner] Scene not found: {scenePath}");
      return false;
    }

    var root = packed.Instantiate<Node>();
    try {
      return RunLoaded(root, scenePath);
    }
    finally {
      root.Free();
    }
  }

  public bool RunLoaded(Node root, string scenePath = DefaultScenePath) {
    try {
      ExportNavMesh(root);
      ExportStaticColliders(root);
      ExportMapLayout(root);
    }
    catch (Exception ex) {
      GD.PushError(ex.Message);
      return false;
    }

    EditorInterface.Singleton?.GetResourceFilesystem()?.Scan();
    GD.Print($"[AvalonBuildExportRunner] Build exports complete for {scenePath}");
    return true;
  }

  private void ScanResourceFilesystem() {
    EditorInterface.Singleton?.GetResourceFilesystem()?.Scan();
  }

  private static void ExportNavMesh(Node root) {
    var region = FindFirst<NavigationRegion3D>(root);
    if (region == null) throw new InvalidDataException("[AvalonBuildExportRunner] No NavigationRegion3D found.");
    if (region.NavigationMesh == null)
      throw new InvalidDataException("[AvalonBuildExportRunner] NavigationRegion3D has no NavigationMesh resource.");

    var exporter = new GodotFPNavMeshExporter();
    var originalName = region.Name;

    // Sealed variant: current scene state, gates block movement. This is the default bake and
    // keeps the existing "NavigationRegion3D.NavMeshData.bytes" filename/consumer unchanged.
    GD.Print("[AvalonBuildExportRunner] Baking Sealed navmesh (gates blocking)...");
    region.BakeNavigationMesh(false);
    exporter.ExportNavMesh(region);

    var gates = region.GetNodeOrNull<Node3D>(GatesNodeName);
    if (gates == null) {
      GD.PushWarning(
        $"[AvalonBuildExportRunner] No '{GatesNodeName}' node found under NavigationRegion3D; skipping Open navmesh variant.");
      return;
    }

    // Open variant: bake parsing walks the scene tree directly and does not respect
    // MeshInstance3D.Visible or CollisionShape3D.Disabled, so the only reliable way to exclude
    // the gates is to detach the node from the tree for the rebake, then reattach it at its
    // original index (index matters: static collider IDs are assigned in tree-walk order).
    var gatesParent = gates.GetParent();
    var gatesIndex = gates.GetIndex();
    gatesParent.RemoveChild(gates);
    region.Name = $"{originalName}{OpenNavMeshSuffix}";
    try {
      GD.Print("[AvalonBuildExportRunner] Baking Open navmesh (gates removed)...");
      region.BakeNavigationMesh(false);
      exporter.ExportNavMesh(region);
    }
    finally {
      region.Name = originalName;
      gatesParent.AddChild(gates);
      gatesParent.MoveChild(gates, gatesIndex);
      region.BakeNavigationMesh(false);
    }
  }

  private static void ExportStaticColliders(Node root) {
    var colliders = new List<FPStaticCollider>();
    var skippedUnsupported = 0;
    CollectStaticColliders(root, colliders, ref skippedUnsupported);

    if (colliders.Count == 0)
      GD.PushWarning(
        $"[AvalonBuildExportRunner] No supported static colliders found; writing empty export. Unsupported skipped: {skippedUnsupported}");
    else
      AssignStaticColliderIds(colliders);

    var sceneRes = root.SceneFilePath;
    var dir = string.IsNullOrEmpty(sceneRes) ? "res://" : sceneRes.GetBaseDir();
    string sceneName = string.IsNullOrEmpty(sceneRes) ? root.Name : sceneRes.GetFile().GetBaseName();
    var bytesRes = dir.PathJoin($"{sceneName}.StaticColliders.bytes");
    var jsonRes = bytesRes.GetBaseName() + ".json";

    FPStaticColliderSerializer.Save(colliders.ToArray(), ProjectSettings.GlobalizePath(bytesRes));
    File.WriteAllText(ProjectSettings.GlobalizePath(jsonRes), FPStaticColliderSerializer.ToJson(colliders),
      Encoding.UTF8);
    LogStaticColliderSummary(colliders, bytesRes, skippedUnsupported);
  }

  private static void ExportMapLayout(Node root) {
    var types = new List<int>();
    var teams = new List<int>();
    var positions = new List<FPVector3>();
    var values = new List<int>();

    CollectMapMarkers(root, types, teams, positions, values);
    if (types.Count == 0) throw new InvalidDataException("[AvalonBuildExportRunner] No SimMarkerNode instances found.");

    var asset = new MapLayoutAsset {
      MarkerTypes = types.ToArray(),
      MarkerTeams = teams.ToArray(),
      MarkerPositions = positions.ToArray(),
      MarkerValues = values.ToArray()
    };

    var serializables = new List<IDataAssetSerializable> { asset };
    var bytes = DataAssetWriter.SerializeMixedCollectionToBytes(serializables);
    File.WriteAllBytes(ProjectSettings.GlobalizePath(MapLayoutBytesPath), bytes);

    var json = DataAssetJsonSerializer.SerializeMixedCollection(new List<IDataAsset> { asset });
    File.WriteAllText(ProjectSettings.GlobalizePath(MapLayoutJsonPath), json);
    GD.Print($"[AvalonBuildExportRunner] Exported {asset.MarkerTypes.Length} markers -> {MapLayoutBytesPath}");
  }

  private static T FindFirst<T>(Node node) where T : Node {
    if (node is T found) return found;

    foreach (var child in node.GetChildren()) {
      var childFound = FindFirst<T>(child);
      if (childFound != null) return childFound;
    }

    return null;
  }

  private static void CollectStaticColliders(Node node, List<FPStaticCollider> colliders, ref int skippedUnsupported) {
    if (node is CollisionShape3D shapeNode) {
      var isStatic = shapeNode.GetParent() is StaticBody3D;
      var isTrigger = shapeNode.GetParent() is Area3D;
      if ((isStatic || isTrigger) && !shapeNode.Disabled && shapeNode.Shape != null) {
        if (shapeNode.Shape is ConvexPolygonShape3D)
          skippedUnsupported++;
        else
          colliders.Add(GodotFPStaticColliderConverter.Convert(shapeNode, isTrigger));
      }
    }

    foreach (var child in node.GetChildren()) CollectStaticColliders(child, colliders, ref skippedUnsupported);
  }

  private static void AssignStaticColliderIds(List<FPStaticCollider> colliders) {
    var next = 1;
    foreach (var collider in colliders)
      if (collider.id > 0 && collider.id >= next)
        next = collider.id + 1;

    for (var i = 0; i < colliders.Count; i++) {
      if (colliders[i].id != -1) continue;

      var collider = colliders[i];
      collider.id = next++;
      colliders[i] = collider;
    }
  }

  private static void
    LogStaticColliderSummary(List<FPStaticCollider> colliders, string outPath, int skippedUnsupported) {
    var sphere = 0;
    var box = 0;
    var capsule = 0;
    var mesh = 0;
    var trigger = 0;

    foreach (var collider in colliders) {
      switch (collider.collider.type) {
        case ShapeType.Sphere:
          sphere++;
          break;
        case ShapeType.Box:
          box++;
          break;
        case ShapeType.Capsule:
          capsule++;
          break;
        case ShapeType.Mesh:
          mesh++;
          break;
      }

      if (collider.isTrigger) trigger++;
    }

    GD.Print($"[AvalonBuildExportRunner] Saved {colliders.Count} colliders -> {outPath}");
    GD.Print(
      $"  Sphere:{sphere} Box:{box} Capsule:{capsule} Mesh:{mesh} Trigger:{trigger} UnsupportedSkipped:{skippedUnsupported}");
  }

  private static void CollectMapMarkers(Node node, List<int> types, List<int> teams, List<FPVector3> positions,
    List<int> values) {
    if (node is Client.Scripts.SimMarkerNode marker) {
      types.Add((int)marker.MarkerType);
      teams.Add(ResolveTeam(marker));
      positions.Add(marker.GlobalTransform.Origin.ToFPVector3());
      values.Add(marker.Value);
    }

    foreach (var child in node.GetChildren()) CollectMapMarkers(child, types, teams, positions, values);
  }

  // Mirrors GodotFPMapLayoutExporter.ResolveTeam: nested SimMarker children (Crystal.tscn,
  // Turret.tscn) silently lose Team overrides written in the parent scene, so the team is derived
  // from the nearest ancestor named "TeamN" instead. Falls back to the export field for markers
  // that aren't organized under a TeamN folder (e.g. neutral markers like Oasis/Pickup).
  private static readonly Regex TeamFolderPattern = new(@"^Team(\d+)$", RegexOptions.Compiled);

  private static int ResolveTeam(Client.Scripts.SimMarkerNode marker) {
    for (var ancestor = marker.GetParent(); ancestor != null; ancestor = ancestor.GetParent()) {
      var match = TeamFolderPattern.Match(ancestor.Name);
      if (match.Success)
        return int.Parse(match.Groups[1].Value);
    }

    return marker.Team;
  }
}
#endif
