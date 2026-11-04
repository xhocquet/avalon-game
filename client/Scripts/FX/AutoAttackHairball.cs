using Godot;
using xpTURN.Klotho.Godot;

namespace Meesles.Avalon.Client.Scripts.View;

public partial class AutoAttackHairball : Node3D {
  private Vector3 _origin;
  private Vector3 _destination;
  private EntityViewNode _target;
  private float _duration;
  private float _elapsed;
  private HairballEffect _ball;

  public void Start(PackedScene scene, Vector3 origin, Vector3 destination, EntityViewNode target, float duration) {
    _origin = origin + Vector3.Up;
    _destination = destination + Vector3.Up;
    _target = target;
    _duration = Mathf.Max(duration, 0.01f);
    _ball = scene.Instantiate<HairballEffect>();
    AddChild(_ball);
    _ball.Configure(0.2f);
    GlobalPosition = _origin;
  }

  public override void _Process(double delta) {
    _elapsed += (float)delta;
    if (_elapsed >= _duration) { QueueFree(); return; }
    if (IsInstanceValid(_target) && _target.IsInsideTree()) _destination = _target.GlobalPosition + Vector3.Up;
    GlobalPosition = _origin.Lerp(_destination, Mathf.Clamp(_elapsed / _duration, 0, 1));
    _ball.Rotate(_ball.SpinAxis, _ball.SpinRadiansPerSecond * (float)delta);
  }
}
