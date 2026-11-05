using Godot;

namespace Meesles.Avalon.Client;

[Tool]
public partial class MinimapHeroMarker : MeshInstance3D {
  private Texture2D _portrait;
  private ShaderMaterial _material;

  [Export]
  public Texture2D Portrait {
    get => _portrait;
    set {
      _portrait = value;
      ApplyPortrait();
    }
  }

  public override void _Ready() {
    Visible = !Godot.Engine.IsEditorHint();
    ApplyPortrait();
  }

  private ShaderMaterial MarkerMaterial {
    get {
      if (_material == null && MaterialOverride is ShaderMaterial source) {
        _material = (ShaderMaterial)source.Duplicate();
        MaterialOverride = _material;
      }
      return _material;
    }
  }

  private void ApplyPortrait() {
    if (_portrait != null)
      MarkerMaterial?.SetShaderParameter("portrait", _portrait);
  }

  public void SetTeamColor(Color color) {
    MarkerMaterial?.SetShaderParameter("team_color", color);
  }
}
