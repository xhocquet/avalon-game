using System;
using Godot;

namespace Meesles.Avalon.Client.Scripts.View;

public partial class SprintSkillEffect : Node3D {
  [Export] public float DurationSeconds { get; set; } = 1f;

  public Action<SprintSkillEffect> Finished;
  private AnimationPlayer _animation;
  private Node3D _follow;
  private float _elapsed;

  public override void _Ready() {
    _animation = GetNode<AnimationPlayer>("Visual/AnimationPlayer");
    ResetVisual();
  }

  public void Play(Vector3 position, float radius, Node3D follow = null) {
    ResetVisual();
    _follow = follow;
    GlobalPosition = follow != null ? follow.GlobalPosition : position;
    Scale = Vector3.One * radius;
    _animation.Play("Appearance");
    _animation.Pause();
    _animation.Seek(0, true);
    Visible = true;
    SetProcess(true);
  }

  public override void _Process(double delta) {
    if (_follow != null && GodotObject.IsInstanceValid(_follow))
      GlobalPosition = _follow.GlobalPosition;
    _elapsed += (float)delta;
    var progress = Mathf.Clamp(_elapsed / Mathf.Max(DurationSeconds, 0.01f), 0f, 1f);
    _animation.Seek(progress * _animation.CurrentAnimationLength, true);
    if (progress >= 1f) {
      ResetVisual();
      Finished?.Invoke(this);
    }
  }

  public void ResetVisual() {
    SetProcess(false);
    Visible = false;
    _elapsed = 0f;
    _follow = null;
    _animation?.Stop();
  }
}
