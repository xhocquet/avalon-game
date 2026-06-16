using global::Godot;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Godot;

namespace Meesles.Avalon
{
  public partial class PlayerView : EntityViewNode
  {
    private int _ownerId = -1;

    public override void OnActivate(FrameRef frame)
    {
      base.OnActivate(frame);

      var live = frame.Frame;
      if (live != null && live.Has<OwnerComponent>(EntityRef))
        _ownerId = live.GetReadOnly<OwnerComponent>(EntityRef).OwnerId;

      if (live != null && live.Has<PlayerComponent>(EntityRef))
      {
        int playerId = live.GetReadOnly<PlayerComponent>(EntityRef).PlayerId;
        var mesh = GetNodeOrNull<MeshInstance3D>("Mesh");
        if (mesh != null)
        {
          mesh.MaterialOverride = new StandardMaterial3D
          {
            AlbedoColor = playerId == 1 ? new Color(0.28f, 0.55f, 0.95f) : new Color(0.9f, 0.3f, 0.25f),
          };
        }
      }
    }

    public override bool OwnerMatches(int ownerId)
    {
      return _ownerId == ownerId;
    }
  }
}
