using System;
using Godot;

namespace Meesles.Avalon.Client.Scripts.View;

public partial class HealingSkillEffect : Node3D {
  [Export] public float DurationSeconds { get; set; } = 2f;
  [Export] public float Height { get; set; } = 2f;
  [Export] public float RadiusMultiplier { get; set; } = 1f;
  [Export] public float InitialRadiusFraction { get; set; } = 0.8f;

  public Action<HealingSkillEffect> Finished;
  private MeshInstance3D _mesh;
  private ShaderMaterial _material;
  private AnimationPlayer _animation;
  private Node3D _ground;
  private Node3D _groundArea;
  private Resource _groundFamily;
  private Node3D _follow;
  private Vector3 _fullScale;
  private float _elapsed;

  public override void _Ready() {
    _mesh = GetNode<MeshInstance3D>("Mesh");
    _material = (ShaderMaterial)_mesh.MaterialOverride.Duplicate();
    _mesh.MaterialOverride = _material;
    _animation = GetNode<AnimationPlayer>("AnimationPlayer");
    _ground = GetNodeOrNull<Node3D>("HealingArea");
    if (_ground != null) {
      _groundArea = _ground.GetNode<Node3D>("Disc");
      _groundFamily = (Resource)_ground.Get("family").As<Resource>().Duplicate();
      _groundFamily.Set("internal_name", new StringName($"HealingArea{GetInstanceId()}"));
      _ground.Set("family", _groundFamily);
    }
    ResetVisual();
  }

  public void Play(Vector3 position, float radius, Node3D follow = null) {
    ResetVisual();
    if (_ground != null) {
      _groundArea.Set("radius", radius);
      _groundArea.Set("extents", new Vector3(radius, 4f, radius));
      _ground.Visible = true;
    }
    var bounds = _mesh.GetAabb();
    var baseRadius = Mathf.Max(bounds.Size.X, bounds.Size.Z) * 0.5f;
    radius *= RadiusMultiplier;
    _fullScale = new Vector3(radius / baseRadius, Height / bounds.Size.Y, radius / baseRadius);
    _mesh.Position = Vector3.Up * -bounds.Position.Y * _fullScale.Y;
    _follow = follow;
    GlobalPosition = follow != null ? follow.GlobalPosition : position;
    _animation.Play("Channel");
    Visible = true;
    ApplyVisual(0f);
    SetProcess(true);
  }

  public override void _Process(double delta) {
    if (_follow != null && GodotObject.IsInstanceValid(_follow))
      GlobalPosition = _follow.GlobalPosition;
    _elapsed += (float)delta;
    var progress = Mathf.Clamp(_elapsed / Mathf.Max(DurationSeconds, 0.01f), 0f, 1f);
    ApplyVisual(progress);
    if (progress >= 1f) {
      ResetVisual();
      Finished?.Invoke(this);
    }
  }

  public void ResetVisual() {
    SetProcess(false);
    Visible = false;
    if (_ground != null) _ground.Visible = false;
    _elapsed = 0f;
    _follow = null;
    _animation?.Stop();
    if (_mesh != null) _mesh.Rotation = Vector3.Zero;
    _material?.SetShaderParameter("tint", new Color(0.3f, 1f, 0.45f, 0f));
  }

  private void ApplyVisual(float progress) {
    var expansion = Mathf.Lerp(InitialRadiusFraction, 1f, 1f - Mathf.Pow(1f - progress, 3f));
    _mesh.Scale = new Vector3(_fullScale.X * expansion, _fullScale.Y, _fullScale.Z * expansion);
    var opacity = 1f - Mathf.SmoothStep(0.25f, 1f, progress);
    _material.SetShaderParameter("tint", new Color(0.3f, 1f, 0.45f, opacity));
    if (_groundFamily != null) {
      var color = new Color(0.6f, 1f, 0.65f, 0.2f * opacity);
      _groundFamily.Set("base_color", color);
      _groundFamily.Set("fill_color", color);
    }
  }
}
