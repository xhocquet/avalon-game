using global::Godot;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Godot;

namespace Meesles.Avalon {
  public partial class HealthBars : ColorRect {
    [Export] public float BarWidth { get; set; } = 54.0f;
    [Export] public float BarHeight { get; set; } = 8.0f;
    [Export] public float BarWorldYOffset { get; set; } = 1.5f;
    [Export] public float BarScreenOffsetY { get; set; } = -10.0f;
    [Export] public Color BackgroundColor { get; set; } = new Color(0f, 0f, 0f, 0.45f);
    [Export] public Color FrameColor { get; set; } = new Color(0f, 0f, 0f, 0.75f);

    public override void _Ready() {
      MouseFilter = MouseFilterEnum.Ignore;
      Color = new Color(0f, 0f, 0f, 0f);
      SetProcess(true);
    }

    public override void _Process(double delta) {
      QueueRedraw();
    }

    public override void _Draw() {
      var cam = GetViewport()?.GetCamera3D();
      if (cam == null) return;
      var vr = GetViewport().GetVisibleRect();

      foreach (Node node in GetTree().GetNodesInGroup("units")) {
        if (node is not Node3D node3d || !IsInstanceValid(node3d)) continue;
        if (node is not EntityViewNode evn) continue;

        var frame = evn.Engine?.PredictedFrame.Frame;
        if (frame == null) continue;

        // TODO: read a Health component here when added to the sim; defaults to full health
        float ratio = 1.0f;

        var worldPoint = node3d.GlobalPosition + new Vector3(0f, BarWorldYOffset, 0f);
        if (cam.IsPositionBehind(worldPoint)) continue;
        var screenPoint = cam.UnprojectPosition(worldPoint);
        var localPoint = screenPoint - vr.Position;

        float x = localPoint.X - BarWidth * 0.5f;
        float y = localPoint.Y + BarScreenOffsetY;

        var rectBg = new Rect2(x, y, BarWidth, BarHeight);
        var rectFill = new Rect2(x, y, BarWidth * ratio, BarHeight);
        var fillColor = GetColorForEntity(evn, frame);

        DrawRect(rectBg, BackgroundColor);
        DrawRect(rectBg, FrameColor, filled: false, width: 1f);
        DrawRect(rectFill, fillColor);
      }
    }

    private static Color GetColorForEntity(EntityViewNode evn, Frame frame) {
      if (frame.Has<Team>(evn.EntityRef))
        return TeamColors.Get(frame.GetReadOnly<Team>(evn.EntityRef).TeamId);
      return new Color(0.4f, 0.8f, 0.3f);
    }
  }
}
