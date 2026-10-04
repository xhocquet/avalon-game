using Godot;

namespace Meesles.Avalon.Client.Scripts.View;

public partial class HairballImpactEffect : Node3D {
  public override void _Ready() {
    GetNode<GpuParticles3D>("Curls").Restart();
    BurstHairball();
    var tween = CreateTween();
    tween.TweenInterval(0.6);
    tween.TweenCallback(Callable.From(QueueFree));
  }

  private void BurstHairball() {
    var burst = GetNode<Node3D>("HairBurst");
    var index = 0;
    foreach (var child in burst.GetChildren()) {
      if (child is not MeshInstance3D mesh) continue;
      if (mesh.MaterialOverride is ShaderMaterial source) {
        var material = (ShaderMaterial)source.Duplicate();
        material.SetShaderParameter("magic_amount", 0f);
        mesh.MaterialOverride = material;
      }

      var scale = mesh.Scale;
      var tween = CreateTween();
      tween.TweenProperty(mesh, "scale", scale * 1.5f, 0.09)
        .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
      tween.TweenProperty(mesh, "scale", Vector3.Zero, 0.32)
        .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);

      if (mesh.Name == "Core") continue;
      var direction = mesh.Basis.Y.Normalized() * (index++ % 2 == 0 ? 1f : -1f);
      var movement = CreateTween().SetParallel();
      movement.TweenProperty(mesh, "position", mesh.Position + direction * 0.5f, 0.3)
        .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
      movement.TweenProperty(mesh, "rotation", mesh.Rotation + direction * 1.2f, 0.4);
    }
  }
}
