using Godot;
using Meesles.Avalon.Client;

namespace Meesles.Avalon;

public partial class SelectionIndicator : Node3D {
  private StandardMaterial3D _material;

  private MeshInstance3D _ring;

  public override void _Ready() {
    _ring = GetNodeOrNull<MeshInstance3D>("Ring");
    if (_ring?.GetActiveMaterial(0) is StandardMaterial3D source) {
      _material = source.Duplicate() as StandardMaterial3D;
      _ring.SetSurfaceOverrideMaterial(0, _material);
    }

    if (!Engine.IsEditorHint())
      Visible = false;
  }

  public void SetSelected(bool selected) {
    Visible = selected;
  }

  public void SetTeamId(int teamId) {
    SetTint(TeamPalette.Default.ForTeam(teamId, 0.72f));
  }

  private void SetTint(Color color) {
    if (_material == null) return;

    _material.AlbedoColor = color;
    _material.Emission = new Color(color.R, color.G, color.B);
  }
}
