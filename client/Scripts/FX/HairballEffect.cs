using Godot;

namespace Meesles.Avalon.Client.Scripts.View;

public partial class HairballEffect : ProjectileEffect {
  private const float BaseRadius = 0.24f;
  private static readonly Vector3 DiagonalSpinAxis = new Vector3(1f, 1f, 0f).Normalized();

  public override Vector3 SpinAxis => DiagonalSpinAxis;
  public override float SpinRadiansPerSecond => Mathf.Tau * 1.5f;

  public override void Configure(float radius) {
    Scale = Vector3.One * Mathf.Max(radius / BaseRadius, 0.01f);
    Show();
  }
}
