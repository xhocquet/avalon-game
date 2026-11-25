using Godot;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Godot;

namespace Meesles.Avalon.Client.Scripts.View;

public partial class TurretArrowVolley : Node3D {
  [Export] public PackedScene ArrowScene { get; set; }
  [Export(PropertyHint.Range, "1,8,1")] public int ArrowCount { get; set; } = 4;
  [Export(PropertyHint.Range, "0,0.5,0.01")] public float StaggerFraction { get; set; } = 0.24f;
  [Export] public float ArcHeight { get; set; } = 0.7f;
  [Export] public float TargetHeight { get; set; } = 0.8f;
  [Export] public float ImpactSpread { get; set; } = 0.18f;

  private Node3D[] _arrows;
  private Vector3[] _origins;
  private EntityViewNode _target;
  private EntityRef _targetEntity;
  private Vector3 _destination;
  private float _duration;
  private float _elapsed;

  public void Start(Vector3[] origins, Vector3 destination, float duration, EntityViewNode target = null) {
    _destination = destination;
    _duration = Mathf.Max(duration, 0.001f);
    _target = target;
    if (IsInstanceValid(target)) _targetEntity = target.EntityRef;
    _arrows = new Node3D[Mathf.Clamp(ArrowCount, 1, 8)];
    _origins = new Vector3[_arrows.Length];
    for (var i = 0; i < _arrows.Length; i++) {
      _origins[i] = origins[i % origins.Length];
      _arrows[i] = ArrowScene.Instantiate<Node3D>();
      AddChild(_arrows[i]);
      _arrows[i].GlobalPosition = _origins[i];
      _arrows[i].Visible = false;
    }
  }

  public override void _Process(double delta) {
    if (_arrows == null) return;
    _elapsed += (float)delta;
    if (_elapsed >= _duration) { QueueFree(); return; }
    if (IsInstanceValid(_target) && _target.IsInsideTree() && _target.Visible &&
        _target.EntityRef.Equals(_targetEntity))
      _destination = _target.GlobalPosition;

    for (var i = 0; i < _arrows.Length; i++) {
      var delay = _duration * Mathf.Clamp(StaggerFraction, 0, 0.5f) * i / Mathf.Max(1, _arrows.Length - 1);
      var t = Mathf.Clamp((_elapsed - delay) / (_duration - delay), 0, 1);
      var arrow = _arrows[i];
      arrow.Visible = _elapsed >= delay;
      if (!arrow.Visible) continue;
      var angle = i * Mathf.Tau / _arrows.Length;
      var end = _destination + Vector3.Up * TargetHeight +
        new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * ImpactSpread;
      var height = ArcHeight * (1 + i * 0.12f);
      arrow.GlobalPosition = _origins[i].Lerp(end, t) + Vector3.Up * (4 * height * t * (1 - t));
      var tangent = end - _origins[i] + Vector3.Up * (4 * height * (1 - 2 * t));
      if (tangent.LengthSquared() > 0.0001f)
        arrow.LookAt(arrow.GlobalPosition + tangent, Mathf.Abs(tangent.Normalized().Dot(Vector3.Up)) > 0.99f
          ? Vector3.Right : Vector3.Up);
    }
  }
}
