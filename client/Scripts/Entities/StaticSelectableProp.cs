using Godot;
using Meesles.Avalon.Client.Scripts.View;
using Meesles.Avalon.Client.Scripts.Interfaces;
using xpTURN.Klotho.Godot;

namespace Meesles.Avalon.Client.Scripts.Entities;

// Static props add their selection collider in _Ready.
[Tool]
[GlobalClass]
public partial class StaticSelectableProp : EntityViewNode, INamedView {
  [Export] public string PropName { get; set; } = "Prop";

  // Selection size in world metres.
  [Export] public float SelectPickRadius { get; set; }
  [Export] public float SelectPickHeight { get; set; }

  public string DisplayName => PropName;

  public override void _Ready() {
    base._Ready();
    if (Godot.Engine.IsEditorHint())
      return;

    EntityViewPhysics.AddSelectionCollider(this, SelectPickRadius, SelectPickHeight);
  }
}
