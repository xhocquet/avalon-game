#if TOOLS
using System.Collections.Generic;
using System.IO;
using System.Text;
using Godot;
using Meesles.Avalon.Sim.Assets;
using xpTURN.Klotho.Deterministic.Geometry;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.Deterministic.Physics;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.ECS.Json;
using xpTURN.Klotho.Godot;

namespace Meesles.Avalon {
  [Tool]
  [GlobalClass]
  public partial class AvalonBuildExportRunner : RefCounted {
    private const string DefaultScenePath = "res://Scenes/World/World.tscn";
    private const string MapLayoutBytesPath = "res://Sim/Data/MapLayout.bytes";
    private const string MapLayoutJsonPath = "res://Sim/Data/MapLayout.json";

    public bool Run(string scenePath = DefaultScenePath) {
      PackedScene packed = ResourceLoader.Load<PackedScene>(scenePath);
      if (packed == null) {
        GD.PushError($"[AvalonBuildExportRunner] Scene not found: {scenePath}");
        return false;
      }

      Node root = packed.Instantiate<Node>();
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
      catch (System.Exception ex) {
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
      if (region == null) {
        throw new InvalidDataException("[AvalonBuildExportRunner] No NavigationRegion3D found.");
      }

      new GodotFPNavMeshExporter().ExportNavMesh(region);
    }

    private static void ExportStaticColliders(Node root) {
      var colliders = new List<FPStaticCollider>();
      int skippedUnsupported = 0;
      CollectStaticColliders(root, colliders, ref skippedUnsupported);

      if (colliders.Count == 0) {
        GD.PushWarning($"[AvalonBuildExportRunner] No supported static colliders found; writing empty export. Unsupported skipped: {skippedUnsupported}");
      }
      else {
        AssignStaticColliderIds(colliders);
      }

      string sceneRes = root.SceneFilePath;
      string dir = string.IsNullOrEmpty(sceneRes) ? "res://" : sceneRes.GetBaseDir();
      string sceneName = string.IsNullOrEmpty(sceneRes) ? root.Name : sceneRes.GetFile().GetBaseName();
      string bytesRes = dir.PathJoin($"{sceneName}.StaticColliders.bytes");
      string jsonRes = bytesRes.GetBaseName() + ".json";

      FPStaticColliderSerializer.Save(colliders.ToArray(), ProjectSettings.GlobalizePath(bytesRes));
      File.WriteAllText(ProjectSettings.GlobalizePath(jsonRes), FPStaticColliderSerializer.ToJson(colliders), Encoding.UTF8);
      LogStaticColliderSummary(colliders, bytesRes, skippedUnsupported);
    }

    private static void ExportMapLayout(Node root) {
      var types = new List<int>();
      var teams = new List<int>();
      var positions = new List<FPVector3>();

      CollectMapMarkers(root, types, teams, positions);
      if (types.Count == 0) {
        throw new InvalidDataException("[AvalonBuildExportRunner] No SimMarkerNode instances found.");
      }

      var asset = new MapLayoutAsset {
        MarkerTypes = types.ToArray(),
        MarkerTeams = teams.ToArray(),
        MarkerPositions = positions.ToArray(),
      };

      var serializables = new List<IDataAssetSerializable> { asset };
      byte[] bytes = DataAssetWriter.SerializeMixedCollectionToBytes(serializables);
      File.WriteAllBytes(ProjectSettings.GlobalizePath(MapLayoutBytesPath), bytes);

      string json = DataAssetJsonSerializer.SerializeMixedCollection(new List<IDataAsset> { asset });
      File.WriteAllText(ProjectSettings.GlobalizePath(MapLayoutJsonPath), json);
      GD.Print($"[AvalonBuildExportRunner] Exported {asset.MarkerTypes.Length} markers -> {MapLayoutBytesPath}");
    }

    private static T FindFirst<T>(Node node) where T : Node {
      if (node is T found) {
        return found;
      }

      foreach (Node child in node.GetChildren()) {
        T childFound = FindFirst<T>(child);
        if (childFound != null) {
          return childFound;
        }
      }

      return null;
    }

    private static void CollectStaticColliders(Node node, List<FPStaticCollider> colliders, ref int skippedUnsupported) {
      if (node is CollisionShape3D shapeNode) {
        bool isStatic = shapeNode.GetParent() is StaticBody3D;
        bool isTrigger = shapeNode.GetParent() is Area3D;
        if ((isStatic || isTrigger) && !shapeNode.Disabled && shapeNode.Shape != null) {
          if (shapeNode.Shape is ConvexPolygonShape3D) {
            skippedUnsupported++;
          }
          else {
            colliders.Add(GodotFPStaticColliderConverter.Convert(shapeNode, isTrigger));
          }
        }
      }

      foreach (Node child in node.GetChildren()) {
        CollectStaticColliders(child, colliders, ref skippedUnsupported);
      }
    }

    private static void AssignStaticColliderIds(List<FPStaticCollider> colliders) {
      int next = 1;
      foreach (var collider in colliders) {
        if (collider.id > 0 && collider.id >= next) {
          next = collider.id + 1;
        }
      }

      for (int i = 0; i < colliders.Count; i++) {
        if (colliders[i].id != -1) {
          continue;
        }

        FPStaticCollider collider = colliders[i];
        collider.id = next++;
        colliders[i] = collider;
      }
    }

    private static void LogStaticColliderSummary(List<FPStaticCollider> colliders, string outPath, int skippedUnsupported) {
      int sphere = 0;
      int box = 0;
      int capsule = 0;
      int mesh = 0;
      int trigger = 0;

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

        if (collider.isTrigger) {
          trigger++;
        }
      }

      GD.Print($"[AvalonBuildExportRunner] Saved {colliders.Count} colliders -> {outPath}");
      GD.Print($"  Sphere:{sphere} Box:{box} Capsule:{capsule} Mesh:{mesh} Trigger:{trigger} UnsupportedSkipped:{skippedUnsupported}");
    }

    private static void CollectMapMarkers(Node node, List<int> types, List<int> teams, List<FPVector3> positions) {
      if (node is SimMarkerNode marker) {
        types.Add((int)marker.MarkerType);
        teams.Add(marker.Team);
        positions.Add(marker.GlobalTransform.Origin.ToFPVector3());
      }

      foreach (Node child in node.GetChildren()) {
        CollectMapMarkers(child, types, teams, positions);
      }
    }
  }
}
#endif
