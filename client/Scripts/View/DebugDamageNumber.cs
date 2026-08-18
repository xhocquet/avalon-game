using Godot;

namespace Meesles.Avalon.Client.Scripts.View;

// Floating hit number, spawned beside DebugAttackLine on the same debug layer and self-freeing the
// same way. Placeholder feedback until real damage popups exist.
public partial class DebugDamageNumber : Label3D {
  private const float Duration = 0.8f;
  private const float RiseSpeed = 1.4f; // Metres per second
  private const float HeadOffsetY = 1.8f;
  private const float SpreadRadius = 0.25f; // Jitter, so simultaneous hits don't overlap exactly
  private const int NormalFontSize = 48;
  private const int CritFontSize = 72;

  private static readonly Color NormalColor = new(1f, 0.92f, 0.85f);
  private static readonly Color CritColor = new(1f, 0.78f, 0.15f);

  private Vector3 _spawnPosition;
  private float _elapsed;

  public static DebugDamageNumber Create(float damage, Vector3 targetPosition, bool isCrit) {
    return Create(
      isCrit ? $"{Mathf.RoundToInt(damage)}!" : Mathf.RoundToInt(damage).ToString(),
      targetPosition, isCrit);
  }

  // Same popup with arbitrary text, for the effects that name themselves rather than reporting a
  // number - a consumed proc says which skill landed.
  public static DebugDamageNumber Create(string text, Vector3 targetPosition, bool emphasized) {
    var node = new DebugDamageNumber {
      Text = text,
      FontSize = emphasized ? CritFontSize : NormalFontSize,
      Modulate = emphasized ? CritColor : NormalColor,
      OutlineSize = 12,
      OutlineModulate = new Color(0f, 0f, 0f, 0.7f),
      Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
      NoDepthTest = true,
      FixedSize = true, // Same screen size at any camera distance, like the debug line's flat width
      PixelSize = 0.0006f
    };

    node._spawnPosition = targetPosition + new Vector3(
      (float)GD.RandRange(-SpreadRadius, SpreadRadius),
      HeadOffsetY,
      (float)GD.RandRange(-SpreadRadius, SpreadRadius));
    return node;
  }

  // The spawn point is a world position and the parent's transform is not ours to assume, so it is
  // applied once the node is in the tree.
  public override void _Ready() {
    GlobalPosition = _spawnPosition;
  }

  public override void _Process(double delta) {
    _elapsed += (float)delta;
    if (_elapsed >= Duration) {
      QueueFree();
      return;
    }

    GlobalPosition += new Vector3(0f, RiseSpeed * (float)delta, 0f);

    // Hold the number solid for the first half, then fade - a linear fade from t=0 makes the peak
    // damage spike the hardest thing to read.
    var alpha = Mathf.Clamp(2f - 2f * (_elapsed / Duration), 0f, 1f);
    Modulate = Modulate with { A = alpha };
    OutlineModulate = OutlineModulate with { A = alpha * 0.7f };
  }
}
