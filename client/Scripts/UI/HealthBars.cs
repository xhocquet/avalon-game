using Godot;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Godot;

namespace Meesles.Avalon;

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

    foreach (var node in GetTree().GetNodesInGroup("units")) {
      if (node is not Node3D node3d || !IsInstanceValid(node3d)) continue;
      if (node is not EntityViewNode evn) continue;

      var frame = evn.Engine?.PredictedFrame.Frame;
      if (frame == null) continue;
      if (!evn.EntityRef.IsValid || !frame.Has<Health>(evn.EntityRef)) continue;
      if (!frame.Has<Stats>(evn.EntityRef)) continue;

      ref readonly var health = ref frame.GetReadOnly<Health>(evn.EntityRef);
      var maxHealth = frame.GetReadOnly<Stats>(evn.EntityRef).MaxHealth;
      if (health.Current <= 0 || maxHealth <= 0) continue;

      var ratio = Mathf.Clamp(health.Current / (float)maxHealth, 0.0f, 1.0f);

      var worldPoint = node3d.GlobalPosition + new Vector3(0f, BarWorldYOffset, 0f);
      if (cam.IsPositionBehind(worldPoint)) continue;
      var screenPoint = cam.UnprojectPosition(worldPoint);
      var localPoint = screenPoint - vr.Position;

      var x = localPoint.X - BarWidth * 0.5f;
      var y = localPoint.Y + BarScreenOffsetY;

      var rectBg = new Rect2(x, y, BarWidth, BarHeight);
      var rectFill = new Rect2(x, y, BarWidth * ratio, BarHeight);
      var fillColor = GetFillColor(frame, evn);

      DrawRect(rectBg, BackgroundColor);
      DrawRect(rectBg, FrameColor, false, 1f);
      DrawRect(rectFill, fillColor);
    }
  }

  private Color GetFillColor(Frame frame, EntityViewNode view) {
    if (!frame.Has<Team>(view.EntityRef))
      return NeutralFillColor;

    var teamId = frame.GetReadOnly<Team>(view.EntityRef).TeamId;
    return teamId == 1 ? TeamOneFillColor : teamId == 2 ? TeamTwoFillColor : NeutralFillColor;
  }
}
