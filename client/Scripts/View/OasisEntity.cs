using xpTURN.Klotho.Core;
using xpTURN.Klotho.Godot;

namespace Meesles.Avalon;

public partial class OasisEntity : EntityViewNode {
  public override void OnInitialize() {
    EntityViewPhysics.DisableGodotCollision(this);
  }

  public override void OnActivate(FrameRef frame) {
    // TODO: unique oasis logic
  }
}
