using Godot;

namespace Meesles.Avalon.Client;

public partial class TooltipTextureRect : TextureRect {
  private Vector2 _tooltipPosition;

  public override string _GetTooltip(Vector2 atPosition) {
    _tooltipPosition = atPosition;
    return TooltipText;
  }

  public override Control _MakeCustomTooltip(string forText) => GameTooltip.Create(forText, this, _tooltipPosition);
}
