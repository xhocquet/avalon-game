using Godot;

namespace Meesles.Avalon;

public enum WorldCursor {
  Default,
  Attack,
  Interact
}

public partial class CursorManager : Node {
  public static CursorManager Instance { get; private set; }

  private Texture2D _attackCursor;
  private Texture2D _defaultCursor;
  private Texture2D _interactCursor;
  private WorldCursor _worldCursor;

  public override void _Ready() {
    Instance = this;
    _defaultCursor = GD.Load<Texture2D>("res://Assets/UI/default-cursor.png");
    var textCursor = GD.Load<Texture2D>("res://Assets/UI/text-cursor.png");
    _attackCursor = GD.Load<Texture2D>("res://Assets/UI/attack-cursor.png");
    _interactCursor = GD.Load<Texture2D>("res://Assets/UI/target-cursor.png");
    Input.SetCustomMouseCursor(_defaultCursor, Input.CursorShape.Arrow, new Vector2(4, 4));
    Input.SetCustomMouseCursor(textCursor, Input.CursorShape.Ibeam, new Vector2(16, 16));
  }

  public override void _ExitTree() {
    if (Instance == this)
      Instance = null;
  }

  public void SetWorldCursor(WorldCursor cursor) {
    if (_worldCursor == cursor) return;

    _worldCursor = cursor;
    var texture = cursor switch {
      WorldCursor.Attack => _attackCursor,
      WorldCursor.Interact => _interactCursor,
      _ => _defaultCursor
    };
    var hotspot = cursor == WorldCursor.Interact ? new Vector2(16, 16) : new Vector2(4, 4);
    Input.SetCustomMouseCursor(texture, Input.CursorShape.Arrow, hotspot);
  }
}
