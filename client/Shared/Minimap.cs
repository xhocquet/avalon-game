using global::Godot;
using System.Collections.Generic;
using xpTURN.Klotho.Godot;
using Meesles.Avalon.Sim.Models;

namespace Meesles.Avalon {
  public partial class Minimap : ColorRect {
    private const float BlipRadius = 4.0f;

    [Export] public Vector2 WorldMin { get; set; } = new Vector2(-50.0f, -50.0f);
    [Export] public Vector2 WorldMax { get; set; } = new Vector2(50.0f, 50.0f);

    private Control _overlay;
    private readonly List<(Vector2 Position, Color Color)> _blips = new();

    public override void _Ready() {
      SetupOverlay();
    }

    private void SetupOverlay() {
      _overlay = new Control();
      _overlay.Name = "MinimapOverlay";
      _overlay.SetAnchorsPreset(LayoutPreset.FullRect);
      _overlay.MouseFilter = MouseFilterEnum.Ignore;
      AddChild(_overlay);
      _overlay.Draw += OnOverlayDraw;
    }

    public override void _Process(double delta) {
      UpdateBlips();
      _overlay?.QueueRedraw();
    }

    private void UpdateBlips() {
      _blips.Clear();
      var seen = new HashSet<Node>();
      foreach (string group in new[] { "units", "minion_unit", "player_unit" }) {
        foreach (Node node in GetTree().GetNodesInGroup(group)) {
          if (!seen.Add(node)) continue;
          if (node is not Node3D node3d || !IsInstanceValid(node3d)) continue;
          var worldPos = node3d.GlobalPosition;
          var mapPos = WorldToMinimap(new Vector2(worldPos.X, worldPos.Z));
          _blips.Add((mapPos, GetColorForNode(node)));
        }
      }
    }

    private Color GetColorForNode(Node node) {
      if (node is EntityViewNode evn) {
        var frame = evn.Engine?.PredictedFrame.Frame;
        if (frame != null && frame.Has<Team>(evn.EntityRef))
          return TeamColors.Get(frame.GetReadOnly<Team>(evn.EntityRef).TeamId);
      }

      return new Color(0.8f, 0.8f, 0.8f);
    }

    public Vector2 WorldToMinimap(Vector2 worldXZ) {
      var overlaySize = _overlay?.Size ?? Vector2.One;
      if (overlaySize.X <= 0 || overlaySize.Y <= 0) overlaySize = new Vector2(1f, 1f);
      float rx = WorldMax.X - WorldMin.X;
      float ry = WorldMax.Y - WorldMin.Y;
      float tx = rx != 0f ? (worldXZ.X - WorldMin.X) / rx : 0f;
      float ty = ry != 0f ? (worldXZ.Y - WorldMin.Y) / ry : 0f;
      tx = Mathf.Clamp(tx, 0f, 1f);
      ty = Mathf.Clamp(ty, 0f, 1f);
      // World XZ -> minimap: Z -> X (flipped), X -> Y; origin at bottom-left
      return new Vector2((1f - ty) * overlaySize.X, tx * overlaySize.Y);
    }

    private void OnOverlayDraw() {
      if (_overlay == null) return;
      foreach (var (position, color) in _blips)
        _overlay.DrawCircle(position, BlipRadius, color);
    }
  }
}
