using Godot;
using Meesles.Avalon.Client;

namespace Meesles.Avalon;

public partial class SelectionIndicator : Node3D {
  private StandardMaterial3D _material;

  private MeshInstance3D _ring;
  private MeshInstance3D _pointer;

  public void EnableHeroPointer() {
    if (_pointer == null) {
      var mesh = new ImmediateMesh();
      mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles);
      mesh.SurfaceAddVertex(new Vector3(-0.13f, 0.04f, 0.7f));
      mesh.SurfaceAddVertex(new Vector3(0, 0.04f, 1.02f));
      mesh.SurfaceAddVertex(new Vector3(0.13f, 0.04f, 0.7f));
      mesh.SurfaceEnd();
      _pointer = new MeshInstance3D {
        Name = "FacingPointer",
        Mesh = mesh,
        MaterialOverride = _material,
        CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
      };
      AddChild(_pointer);
      if (_material != null) _material.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
    }
  }

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
