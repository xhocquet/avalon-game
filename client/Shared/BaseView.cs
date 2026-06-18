using global::Godot;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Godot;

namespace Meesles.Avalon {
  public partial class BaseView : EntityViewNode {
    public override void OnActivate(FrameRef frame) {
      base.OnActivate(frame);

      var live = frame.Frame;
      if (live == null || !live.Has<Team>(EntityRef)) return;

      int teamId = live.GetReadOnly<Team>(EntityRef).TeamId;
      var mesh = GetNodeOrNull<MeshInstance3D>("Mesh");
      if (mesh == null) return;

      mesh.MaterialOverride = new StandardMaterial3D {
        AlbedoColor = GetTeamColor(teamId),
      };
    }

    private static Color GetTeamColor(int teamId) {
      return teamId switch {
        1 => new Color(0.28f, 0.55f, 0.95f),
        2 => new Color(0.9f, 0.3f, 0.25f),
        3 => new Color(0.3f, 0.8f, 0.35f),
        4 => new Color(0.95f, 0.82f, 0.22f),
        _ => new Color(0.8f, 0.8f, 0.8f),
      };
    }
  }
}
