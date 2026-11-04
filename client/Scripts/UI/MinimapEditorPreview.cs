using Godot;

namespace Meesles.Avalon.Client;

[Tool]
public partial class MinimapEditorPreview : Control {
  public override void _Ready() {
    Visible = Engine.IsEditorHint();
    Resized += QueueRedraw;
  }

  public override void _Draw() {
    if (!Engine.IsEditorHint()) return;

    DrawRect(new Rect2(Vector2.Zero, Size), new Color(0.12f, 0.17f, 0.14f));
    var gridColor = new Color(0.24f, 0.3f, 0.25f, 0.5f);
    for (var i = 1; i < 8; i++) {
      var x = Size.X * i / 8f;
      var y = Size.Y * i / 8f;
      DrawLine(new Vector2(x, 0), new Vector2(x, Size.Y), gridColor);
      DrawLine(new Vector2(0, y), new Vector2(Size.X, y), gridColor);
    }

    const string text = "MINIMAP";
    var font = ThemeDB.FallbackFont;
    var textSize = font.GetStringSize(text, fontSize: 16);
    DrawString(font, new Vector2((Size.X - textSize.X) / 2, Size.Y / 2 + font.GetAscent(16) / 2),
      text, fontSize: 16, modulate: new Color(0.64f, 0.7f, 0.63f));
  }
}
