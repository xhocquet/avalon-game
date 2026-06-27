using global::Godot;
using Meesles.Avalon.Sim.Models;
using xpTURN.Klotho.Godot;

namespace Meesles.Avalon {
  public partial class HealthBars : ColorRect {
    [Export] public float BarWidth { get; set; } = 54.0f;
    [Export] public float BarHeight { get; set; } = 8.0f;
    [Export] public float BarWorldYOffset { get; set; } = 1.5f;
    [Export] public float BarScreenOffsetY { get; set; } = -10.0f;
    [Export] public Color BackgroundColor { get; set; } = new(0f, 0f, 0f, 0.45f);
    [Export] public Color FrameColor { get; set; } = new(0f, 0f, 0f, 0.75f);
    [Export] public Color TeamOneFillColor { get; set; } = new(0.25f, 0.75f, 0.95f);
    [Export] public Color TeamTwoFillColor { get; set; } = new(0.95f, 0.35f, 0.28f);
    [Export] public Color NeutralFillColor { get; set; } = new(0.55f, 0.85f, 0.35f);

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
        if (!evn.EntityRef.IsValid || !frame.Has<Health>(evn.EntityRef)) continue;

        ref readonly var health = ref frame.GetReadOnly<Health>(evn.EntityRef);
        if (health.Current <= 0 || health.Max <= 0) continue;

        float ratio = Mathf.Clamp(health.Current / (float)health.Max, 0.0f, 1.0f);

        var worldPoint = node3d.GlobalPosition + new Vector3(0f, BarWorldYOffset, 0f);
        if (cam.IsPositionBehind(worldPoint)) continue;
        var screenPoint = cam.UnprojectPosition(worldPoint);
        var localPoint = screenPoint - vr.Position;

        float x = localPoint.X - BarWidth * 0.5f;
        float y = localPoint.Y + BarScreenOffsetY;

        var rectBg = new Rect2(x, y, BarWidth, BarHeight);
        var rectFill = new Rect2(x, y, BarWidth * ratio, BarHeight);
        var fillColor = GetFillColor(frame, evn);

        DrawRect(rectBg, BackgroundColor);
        DrawRect(rectBg, FrameColor, filled: false, width: 1f);
        DrawRect(rectFill, fillColor);
      }
    }

    private Color GetFillColor(xpTURN.Klotho.ECS.Frame frame, EntityViewNode view) {
      if (!frame.Has<Team>(view.EntityRef))
        return NeutralFillColor;

      int teamId = frame.GetReadOnly<Team>(view.EntityRef).TeamId;
      return teamId == 1 ? TeamOneFillColor : teamId == 2 ? TeamTwoFillColor : NeutralFillColor;
    }
  }
}
