using System;
using Godot;

namespace Meesles.Avalon.Client;

public static class GameTooltip {
  private const float MaxTextWidth = 320f;

  public static Control Create(string text, Control source, Vector2 cursorPosition) {
    var existing = source.GetNodeOrNull<TooltipContent>("Tooltip");
    if (string.IsNullOrWhiteSpace(text)) {
      existing?.Dismiss();
      return null;
    }
    if (existing != null && !existing.IsQueuedForDeletion() && existing.Text == text) {
      existing.PositionTooltip(cursorPosition);
    }
    else {
      if (existing != null) {
        existing.Dismiss();
        source.RemoveChild(existing);
      }
      source.AddChild(new TooltipContent {
        Name = "Tooltip",
        Layer = 1000,
        Source = source,
        Text = text,
        InitialCursorPosition = cursorPosition
      });
    }
    return new Control { Visible = false }; // Suppress the native popup.
  }

  public static Control CreateContent(string text) {
    var sections = text.Replace("\r", "").Split("\n\n", 2, StringSplitOptions.None);
    var heading = sections[0].Split('\n', 2);
    var content = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
    content.AddThemeConstantOverride("separation", 6);
    var title = CreateLabel(heading[0], 18, new Color(1f, 0.88f, 0.65f));
    if (heading.Length > 1 && heading[1].StartsWith("Cooldown: ", StringComparison.Ordinal)) {
      var header = new HBoxContainer {
        CustomMinimumSize = new Vector2(MaxTextWidth, 0),
        SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        MouseFilter = Control.MouseFilterEnum.Ignore
      };
      header.AddThemeConstantOverride("separation", 16);
      title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
      header.AddChild(title);
      var cooldown = CreateLabel(heading[1], 13, new Color(0.65f, 0.65f, 0.65f));
      cooldown.AutowrapMode = TextServer.AutowrapMode.Off;
      cooldown.HorizontalAlignment = HorizontalAlignment.Right;
      cooldown.VerticalAlignment = VerticalAlignment.Center;
      title.CustomMinimumSize = new Vector2(
        Math.Min(title.CustomMinimumSize.X, MaxTextWidth - cooldown.GetMinimumSize().X - 16), 0);
      header.AddChild(cooldown);
      content.AddChild(header);
    }
    else {
      content.AddChild(title);
      if (heading.Length > 1)
        content.AddChild(CreateLabel(heading[1], 14, new Color(0.8f, 0.77f, 0.7f)));
    }
    if (sections.Length > 1) {
      var body = CreateLabel(sections[1], 16, new Color(0.95f, 0.93f, 0.88f));
      body.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
      body.AddThemeConstantOverride("line_spacing", 3);
      content.AddChild(body);
    }

    return content;
  }

  private static Label CreateLabel(string text, int fontSize, Color color) {
    var label = new Label { Text = text, MouseFilter = Control.MouseFilterEnum.Ignore };
    label.AddThemeFontSizeOverride("font_size", fontSize);
    label.AddThemeColorOverride("font_color", color);
    var width = 0f;
    foreach (var line in text.Split('\n'))
      width = Math.Max(width, label.GetThemeFont("font").GetStringSize(line, fontSize: fontSize).X);
    label.CustomMinimumSize = new Vector2(Math.Min(width, MaxTextWidth), 0);
    label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
    return label;
  }
}
