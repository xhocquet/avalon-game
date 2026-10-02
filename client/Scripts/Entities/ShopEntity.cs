using Godot;
using Meesles.Avalon.Client.Scripts.Interfaces;

namespace Meesles.Avalon.Client.Scripts.Entities;

// Static shops initialize selection and team state in _Ready.
[Tool]
[GlobalClass]
public partial class ShopEntity : TeamEntityViewNode, INamedView {
  // Hotkeys find static shops through this group.
  public const string ShopsGroup = "shops";

  public string DisplayName => "Shop";

  // Static shops get their team from World.tscn.
  [Export] public int Team { get; set; } = -1;

  // Selection size in world metres.
  [Export] public float SelectPickRadius { get; set; } = -1.0f;
  [Export] public float SelectPickHeight { get; set; } = -1.0f;

  public override void _Ready() {
    base._Ready();
    if (Godot.Engine.IsEditorHint())
      return;

    EntityViewPhysics.AddSelectionCollider(this, SelectPickRadius, SelectPickHeight);
    AddToGroup(ShopsGroup);
    SetTeam(Team);
  }
}
