using global::Godot;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Godot;
using Meesles.Avalon.Sim.Models;

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
        AlbedoColor = TeamColors.Get(teamId),
      };
    }
  }
}
