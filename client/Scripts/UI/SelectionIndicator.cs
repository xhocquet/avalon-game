using global::Godot;

namespace Meesles.Avalon {
  public partial class SelectionIndicator : Node3D {
    private static readonly Color TeamOneColor = new(0.25f, 0.75f, 0.95f, 0.72f);
    private static readonly Color TeamTwoColor = new(0.95f, 0.35f, 0.28f, 0.72f);
    private static readonly Color NeutralColor = new(0.55f, 0.85f, 0.35f, 0.72f);

    private MeshInstance3D _ring;
    private StandardMaterial3D _material;

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
      SetTint(teamId == 1 ? TeamOneColor : teamId == 2 ? TeamTwoColor : NeutralColor);
    }

    private void SetTint(Color color) {
      if (_material == null) return;

      _material.AlbedoColor = color;
      _material.Emission = new Color(color.R, color.G, color.B, 1.0f);
    }
  }
}
