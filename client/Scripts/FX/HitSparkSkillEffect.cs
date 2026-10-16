using System;
using Godot;

namespace Meesles.Avalon.Client.Scripts.View;

public partial class HitSparkSkillEffect : Node3D {
  [Export] public float EffectScale { get; set; } = 1f;
  [Export] public Vector3 LocalOffset { get; set; }

  public Action<HitSparkSkillEffect> Finished;
  private GpuParticles3D[] _particles;
  private OmniLight3D _light;
  private float _lightEnergy;
  private float _duration;
  private float _elapsed;
  private Node3D _follow;
  private Tween _flash;

  public override void _Ready() {
    var visual = GetNode<Node3D>("Visual");
    var layers = visual.FindChildren("*", "GPUParticles3D", true, false);
    _particles = new GpuParticles3D[layers.Count];
    for (var i = 0; i < layers.Count; i++) {
      _particles[i] = (GpuParticles3D)layers[i];
      _duration = Mathf.Max(_duration, (float)(_particles[i].Lifetime * (2 - _particles[i].Explosiveness)));
    }
    _light = visual.GetNode<OmniLight3D>("Light");
    _lightEnergy = _light.LightEnergy;
    _duration = Mathf.Max(_duration, _light.GetMeta("vfx_flash").AsSingle());
    ResetVisual();
  }

  public void Play(Vector3 position, float yaw, Node3D follow = null) {
    ResetVisual();
    _follow = follow;
    Scale = Vector3.One * EffectScale;
    ApplyPose(position, yaw);
    Visible = true;
    foreach (var particles in _particles) particles.Restart();
    _light.LightEnergy = _lightEnergy;
    _flash = CreateTween();
    _flash.TweenProperty(_light, "light_energy", 0f, _light.GetMeta("vfx_flash").AsSingle())
      .SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
    SetProcess(true);
  }

  public override void _Process(double delta) {
    if (GodotObject.IsInstanceValid(_follow)) ApplyPose(_follow.GlobalPosition, _follow.GlobalRotation.Y);
    _elapsed += (float)delta;
    if (_elapsed >= _duration) {
      ResetVisual();
      Finished?.Invoke(this);
    }
  }

  public void ResetVisual() {
    SetProcess(false);
    Visible = false;
    _elapsed = 0;
    _follow = null;
    _flash?.Kill();
    _flash = null;
    if (_light != null) _light.LightEnergy = 0;
    if (_particles == null) return;
    foreach (var particles in _particles) particles.Emitting = false;
  }

  private void ApplyPose(Vector3 position, float yaw) {
    if (GodotObject.IsInstanceValid(_follow)) {
      position = _follow.GlobalPosition;
      yaw = _follow.GlobalRotation.Y;
    }
    GlobalPosition = position + new Basis(Vector3.Up, yaw) * LocalOffset;
  }
}
