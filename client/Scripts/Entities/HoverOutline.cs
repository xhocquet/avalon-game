using System.Collections.Generic;
using Godot;

namespace Meesles.Avalon.Client.Scripts.Entities;

internal sealed class HoverOutline {
  private readonly Node3D _root;
  private readonly List<MeshInstance3D> _meshes = new();
  private HoverOutlineRenderer _renderer;

  public bool IsHighlighted { get; private set; }
  public bool IsSelected { get; private set; }
  private int _teamId = -1;

  public HoverOutline(Node3D root) {
    _root = root;
    CollectMeshes(root, _meshes);
  }

  public void SetHighlighted(bool highlighted) {
    if (IsHighlighted == highlighted) return;
    IsHighlighted = highlighted;
    UpdateOutline();
  }

  public void SetSelected(bool selected) {
    if (IsSelected == selected) return;
    IsSelected = selected;
    UpdateOutline();
  }

  public void SetTeam(int teamId) {
    if (_teamId == teamId) return;
    _teamId = teamId;
    if (IsSelected || IsHighlighted) UpdateOutline();
  }

  private void UpdateOutline() {
    if (IsHighlighted || IsSelected) {
      var camera = _root.GetViewport().GetCamera3D();
      if (camera == null) return;
      _renderer = HoverOutlineRenderer.ForCamera(camera);
      _renderer.SetTarget(_root, _meshes, _teamId, IsSelected, IsHighlighted);
    }
    else if (GodotObject.IsInstanceValid(_renderer)) {
      _renderer.HideTarget(_root);
    }
  }

  internal static void CollectMeshes(Node node, List<MeshInstance3D> meshes) {
    if (node is SelectionIndicator or GpuParticles3D or CpuParticles3D ||
        node.Name == "MinimapMarker" || node.Name == "SelectionPickArea" ||
        node.IsInGroup("hover_outline_exclude")) return;

    if (node is MeshInstance3D mesh) meshes.Add(mesh);
    foreach (var child in node.GetChildren()) CollectMeshes(child, meshes);
  }
}
