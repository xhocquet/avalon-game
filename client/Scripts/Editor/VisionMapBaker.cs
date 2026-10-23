#if TOOLS
using System;
using System.Collections.Generic;
using Godot;
using Meesles.Avalon.Sim.Assets;
using xpTURN.Klotho.Deterministic.Math;

namespace Meesles.Avalon;

public static class VisionMapBaker {
  public static void Populate(Node root, MapLayoutAsset layout) {
    var region = FindRegion(root) ?? throw new InvalidOperationException("Vision bake requires a NavigationRegion3D.");
    using var mesh = (NavigationMesh)region.NavigationMesh.Duplicate();
    mesh.AgentRadius = mesh.CellSize; // Minimum one-voxel clearance.
    mesh.ClearPolygons();
    using var source = new NavigationMeshSourceGeometryData3D();
    var gates = region.GetNodeOrNull<Node>("Gates");
    var gateIndex = gates?.GetIndex() ?? 0;
    if (gates != null) region.RemoveChild(gates);
    try {
      NavigationServer3D.ParseSourceGeometryData(mesh, source, region);
      NavigationServer3D.BakeFromSourceGeometryData(mesh, source);
    }
    finally {
      if (gates != null) {
        region.AddChild(gates);
        region.MoveChild(gates, gateIndex);
      }
    }

    var vertices = mesh.GetVertices();
    if (vertices.Length == 0 || mesh.GetPolygonCount() == 0)
      throw new InvalidOperationException("Vision bake produced no walkable geometry.");
    var points = new List<FPVector2>();
    var welded = new Dictionary<(long, long), int>();
    var remap = new int[vertices.Length];
    var min = new Vector2(float.MaxValue, float.MaxValue);
    var max = new Vector2(float.MinValue, float.MinValue);
    for (var i = 0; i < vertices.Length; i++) {
      var world = region.GlobalTransform * vertices[i];
      var key = ((long)Math.Round(world.X * 1000), (long)Math.Round(world.Z * 1000));
      if (!welded.TryGetValue(key, out var index)) {
        welded[key] = index = points.Count;
        points.Add(new FPVector2(FP64.FromInt((int)key.Item1) / FP64.FromInt(1000),
          FP64.FromInt((int)key.Item2) / FP64.FromInt(1000)));
      }
      remap[i] = index;
      min = min.Min(new Vector2(world.X, world.Z));
      max = max.Max(new Vector2(world.X, world.Z));
    }

    var counts = new Dictionary<(int, int), int>();
    for (var polygon = 0; polygon < mesh.GetPolygonCount(); polygon++) {
      var indices = mesh.GetPolygon(polygon);
      for (var i = 0; i < indices.Length; i++) {
        var a = remap[indices[i]];
        var b = remap[indices[(i + 1) % indices.Length]];
        if (a == b) continue;
        var edge = a < b ? (a, b) : (b, a);
        counts.TryGetValue(edge, out var count);
        counts[edge] = count + 1;
      }
    }
    var boundaries = new List<(int, int)>();
    foreach (var pair in counts)
      if (pair.Value == 1) boundaries.Add(pair.Key);
    boundaries.Sort();
    var edges = new List<FPVector2>();
    foreach (var (a, b) in boundaries) {
      edges.Add(points[a]);
      edges.Add(points[b]);
    }
    layout.SightEdges = edges.ToArray();
    layout.VisionOrigin = new FPVector2(Mathf.Floor(min.X).ToFP64(), Mathf.Floor(min.Y).ToFP64());
    layout.VisionSize = new FPVector2((Mathf.Ceil(max.X) - Mathf.Floor(min.X)).ToFP64(),
      (Mathf.Ceil(max.Y) - Mathf.Floor(min.Y)).ToFP64());
    layout.VisionCellSize = FP64.FromInt(4); // Matches the deterministic navmesh broad phase.
    GD.Print($"[VisionMapBaker] {layout.MapName}: {edges.Count / 2} sight boundaries");
  }

  private static NavigationRegion3D FindRegion(Node node) {
    if (node is NavigationRegion3D region) return region;
    foreach (var child in node.GetChildren()) {
      var found = FindRegion(child);
      if (found != null) return found;
    }
    return null;
  }
}
#endif
