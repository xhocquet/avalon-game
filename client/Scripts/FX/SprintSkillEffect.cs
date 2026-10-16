using System;
using System.Collections.Generic;
using Godot;

namespace Meesles.Avalon.Client.Scripts.View;

public partial class SprintSkillEffect : Node3D {
  [Export] public float DurationSeconds { get; set; } = 1f;
  [Export] public string AnimationName { get; set; } = "Appearance";
  [Export] public Color Tint { get; set; } = Colors.White;
  [Export] public Godot.Collections.Dictionary<NodePath, Vector3> MeshScaleMultipliers { get; set; } = new();

  public Action<SprintSkillEffect> Finished;
  private AnimationPlayer _animation;
  private Node3D _follow;
  private float _elapsed;

  public override void _Ready() {
    _animation = GetNode<AnimationPlayer>("Visual/AnimationPlayer");
    var materials = new HashSet<ShaderMaterial>();
    foreach (var child in GetNode<Node3D>("Visual").GetChildren()) {
      if (child is MeshInstance3D mesh && mesh.MaterialOverride is ShaderMaterial material &&
          materials.Add(material))
        material.SetShaderParameter("tint", material.GetShaderParameter("tint").AsColor() * Tint);
    }
    ResetVisual();
  }

  public void Play(Vector3 position, float radius, Node3D follow = null) {
    ResetVisual();
    _follow = follow;
    GlobalPosition = follow != null ? follow.GlobalPosition : position;
    Scale = Vector3.One * radius;
    _animation.Play(AnimationName);
    _animation.Pause();
    SeekAnimation(0);
    Visible = true;
    SetProcess(true);
  }

  public override void _Process(double delta) {
    if (_follow != null && GodotObject.IsInstanceValid(_follow))
      GlobalPosition = _follow.GlobalPosition;
    _elapsed += (float)delta;
    var progress = Mathf.Clamp(_elapsed / Mathf.Max(DurationSeconds, 0.01f), 0f, 1f);
    SeekAnimation(progress * _animation.CurrentAnimationLength);
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

  private void SeekAnimation(double time) {
    _animation.Seek(time, true);
    foreach (var (path, multiplier) in MeshScaleMultipliers)
      GetNode<Node3D>(path).Scale *= multiplier;
  }
}
