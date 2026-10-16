using System;
using Godot;

namespace Meesles.Avalon.Client.Scripts.View;

public partial class SheenSkillEffect : Node3D {
  [Export] public float DurationSeconds { get; set; } = 0.5f;
  [Export] public float EffectScale { get; set; } = 1f;
  [Export] public Vector3 LocalOffset { get; set; } = new(0.65f, 1.45f, 0.45f);

  public Action<SheenSkillEffect> Finished;
  private GpuParticles3D[] _particles;
  private Node3D _follow;
  private float _elapsed;

  public override void _Ready() {
    _particles = [GetNode<GpuParticles3D>("Visual/Sheen"), GetNode<GpuParticles3D>("Visual/Glints")];
    ResetVisual();
  }

  public void Play(Vector3 position, float yaw, Node3D follow = null) {
    ResetVisual();
    _follow = follow;
    Scale = Vector3.One * EffectScale;
    ApplyPose(position, yaw);
    Visible = true;
    foreach (var particles in _particles) {
      particles.OneShot = true;
      particles.Preprocess = 0;
      particles.Explosiveness = 1;
      particles.Lifetime = Mathf.Max(DurationSeconds, 0.01f);
      particles.Restart();
      particles.Emitting = true;
    }
    SetProcess(true);
  }

  public override void _Process(double delta) {
    if (_follow != null && GodotObject.IsInstanceValid(_follow))
      ApplyPose(_follow.GlobalPosition, _follow.GlobalRotation.Y);
    _elapsed += (float)delta;
    if (_elapsed >= Mathf.Max(DurationSeconds, 0.01f)) {
      ResetVisual();
      Finished?.Invoke(this);
    }
  }

  public void ResetVisual() {
    SetProcess(false);
    Visible = false;
    _elapsed = 0;
    _follow = null;
    if (_particles == null) return;
    foreach (var particles in _particles)
      particles.Emitting = false;
  }

  private void ApplyPose(Vector3 position, float yaw) {
    if (_follow != null && GodotObject.IsInstanceValid(_follow)) {
      position = _follow.GlobalPosition;
      yaw = _follow.GlobalRotation.Y;
    }
    GlobalPosition = position + new Basis(Vector3.Up, yaw) * LocalOffset; // +Z is forward.
  }
}
