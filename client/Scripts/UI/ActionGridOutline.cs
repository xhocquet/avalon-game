using Godot;

namespace Meesles.Avalon;

internal static class ActionGridOutline {
  public static Panel Create(string name, Color? borderColor = null) {
    var style = new StyleBoxFlat {
      BgColor = new Color(0f, 0f, 0f, 0f),
      BorderColor = borderColor ?? new Color(1f, 0.25f, 0.08f, 1f)
    };
    style.SetBorderWidthAll(3);

    var panel = new Panel {
      Name = name,
      MouseFilter = Control.MouseFilterEnum.Ignore,
      Visible = false
    };
    panel.SetAnchorsPreset(Control.LayoutPreset.FullRect);
    panel.AddThemeStyleboxOverride("panel", style);
    return panel;
  }
}
