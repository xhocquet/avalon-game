using global::Godot;
using xpTURN.Klotho.Core;
using Meesles.Avalon.Sim.Models;
using xpTURN.Klotho.Godot;

namespace Meesles.Avalon {
  public partial class MinionView : EntityViewNode {
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
