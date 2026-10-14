using Godot;

namespace Meesles.Avalon.Client;

public partial class TooltipContent : CanvasLayer {
  public Control Source { get; set; }
  public string Text { get; set; }
  public Vector2 InitialCursorPosition { get; set; }
  private PanelContainer _panel;
  private Vector2 _cursorPosition;

  public override void _Ready() {
    _cursorPosition = InitialCursorPosition;
    _panel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
    _panel.AddThemeStyleboxOverride("panel", Source.GetThemeStylebox("panel", "TooltipPanel"));
    _panel.AddChild(GameTooltip.CreateContent(Text));
    AddChild(_panel);
    _panel.Resized += OnPanelResized;
    Source.GuiInput += OnSourceGuiInput;
    Source.MouseExited += Dismiss;
    Source.VisibilityChanged += OnSourceVisibilityChanged;
    CallDeferred(MethodName.PlaceInitialTooltip);
  }

  public override void _ExitTree() {
    if (!IsInstanceValid(Source)) return;
    Source.GuiInput -= OnSourceGuiInput;
    Source.MouseExited -= Dismiss;
    Source.VisibilityChanged -= OnSourceVisibilityChanged;
  }

  private void OnSourceVisibilityChanged() {
    if (!Source.IsVisibleInTree()) Dismiss();
  }

  private void PlaceInitialTooltip() {
    PositionTooltip(_cursorPosition);
  }

  private void OnPanelResized() {
    PositionTooltip(_cursorPosition);
  }

  public void Dismiss() {
    Hide();
    QueueFree();
  }

  private void OnSourceGuiInput(InputEvent input) {
    if (input is InputEventMouseButton { Pressed: true }) {
      Dismiss();
      return;
    }
    if (input is not InputEventMouseMotion motion || IsQueuedForDeletion()) return;
    if (!new Rect2(Vector2.Zero, Source.Size).HasPoint(motion.Position)) {
      Dismiss();
      return;
    }

    PositionTooltip(motion.Position);
  }

  public void PositionTooltip(Vector2 localCursor) {
    if (_panel == null || IsQueuedForDeletion()) return;
    _cursorPosition = localCursor;

    var cursor = Source.GetGlobalTransformWithCanvas() * localCursor;
    var offset = ProjectSettings.GetSetting("display/mouse_cursor/tooltip_position_offset").AsVector2();
    var bounds = Source.GetViewport().GetVisibleRect();
    var position = cursor + offset;
    _panel.Size = _panel.GetCombinedMinimumSize();
    var size = _panel.Size;
    if (position.X + size.X > bounds.End.X) position.X = cursor.X - size.X - offset.X;
    if (position.Y + size.Y > bounds.End.Y) position.Y = cursor.Y - size.Y - offset.Y;
    position.X = Mathf.Clamp(position.X, bounds.Position.X, Mathf.Max(bounds.Position.X, bounds.End.X - size.X));
    position.Y = Mathf.Clamp(position.Y, bounds.Position.Y, Mathf.Max(bounds.Position.Y, bounds.End.Y - size.Y));
    _panel.Position = position.Round();
  }
}
