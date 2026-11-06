using System.Collections.Generic;
using Godot;

namespace Meesles.Avalon.Client.Scripts.Entities;

internal sealed class HoverOutline {
  private static ShaderMaterial _material;
  private readonly List<MeshInstance3D> _meshes = new();
  private readonly List<Material> _previousOverlays = new();

  public bool IsHighlighted { get; private set; }

  public HoverOutline(Node root) {
    _material ??= GD.Load<ShaderMaterial>("res://Assets/FX/Shaders/HoverOutline.tres");
    CollectMeshes(root);
  }

  public void SetHighlighted(bool highlighted) {
    if (IsHighlighted == highlighted) return;
    IsHighlighted = highlighted;
    for (var i = 0; i < _meshes.Count; i++) {
      var mesh = _meshes[i];
      if (!GodotObject.IsInstanceValid(mesh)) continue;
      if (highlighted) {
        _previousOverlays[i] = mesh.MaterialOverlay;
        mesh.MaterialOverlay = _material;
      }
      else {
        if (mesh.MaterialOverlay == _material)
          mesh.MaterialOverlay = _previousOverlays[i];
        _previousOverlays[i] = null;
      }
    }
  }

  private void CollectMeshes(Node node) {
    if (node is SelectionIndicator or GpuParticles3D ||
        node.Name == "MinimapMarker" || node.Name == "SelectionPickArea" ||
        node.IsInGroup("hover_outline_exclude")) return;

    if (node is MeshInstance3D mesh) {
      _meshes.Add(mesh);
      _previousOverlays.Add(null);
    }
    foreach (var child in node.GetChildren())
      CollectMeshes(child);
  }
}
