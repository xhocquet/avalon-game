using Godot;

namespace Meesles.Avalon.Client.Scripts.View;

[Tool]
public abstract partial class ProjectileEffect : Node3D {
  public virtual Vector3 SpinAxis => Vector3.Back;
  public virtual float SpinRadiansPerSecond => Mathf.Tau * 3f;

  public abstract void Configure(float radius);

  public void ResetVisual() {
    Hide();
    GlobalPosition = Vector3.Zero;
    GlobalRotation = Vector3.Zero;
    Scale = Vector3.One;
  }
}
