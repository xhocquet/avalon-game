using Godot;

namespace Meesles.Avalon;

public static class HotkeyBadge {
  private static readonly LabelSettings Settings = GD.Load<LabelSettings>("res://Assets/UI/HotkeyLabel.tres");

  public static void Add(Control icon, string text) {
    var label = new Label {
      Name = "Hotkey",
      Text = text,
      LabelSettings = Settings,
      MouseFilter = Control.MouseFilterEnum.Ignore,
      HorizontalAlignment = HorizontalAlignment.Right,
      VerticalAlignment = VerticalAlignment.Bottom
    };
    label.SetAnchorsPreset(Control.LayoutPreset.FullRect);
    label.OffsetLeft = 4f;
    label.OffsetTop = 4f;
    label.OffsetRight = -4f;
    label.OffsetBottom = -4f;
    icon.AddChild(label);
  }
}
