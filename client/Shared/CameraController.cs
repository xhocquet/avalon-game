using global::Godot;

namespace Meesles.Avalon {
  public partial class CameraController : Camera3D {
    [Export] public float Height = 14f;
    [Export] public float BackDistance = 24f;
    [Export] public float PanSpeed = 10f;
    [Export] public float FollowSharpness = 12f;

    private Vector3 _focus = Vector3.Zero;
    private Node3D _followTarget;

    public void SetFollowTarget(Node3D target) {
      _followTarget = target;
      if (_followTarget == null) return;

      _focus = Flatten(_followTarget.GlobalPosition);
      ApplyView();
    }

    public override void _Ready() {
      ApplyView();
    }

    public override void _Process(double delta) {
      float x = Input.GetActionStrength("camera_right") - Input.GetActionStrength("camera_left");
      float z = Input.GetActionStrength("camera_down") - Input.GetActionStrength("camera_up");
      bool hasManualInput = !Mathf.IsZeroApprox(x) || !Mathf.IsZeroApprox(z);

      if (hasManualInput) {
        _focus += new Vector3(x, 0f, z) * PanSpeed * (float)delta;
      }
      else if (_followTarget != null) {
        Vector3 targetFocus = Flatten(_followTarget.GlobalPosition);
        float t = 1f - Mathf.Exp(-FollowSharpness * (float)delta);
        _focus = _focus.Lerp(targetFocus, t);
      }

      ApplyView();
    }

    private void ApplyView() {
      LookAtFromPosition(_focus + new Vector3(0f, Height, BackDistance), _focus, Vector3.Up);
    }

    private static Vector3 Flatten(Vector3 position) {
      return new Vector3(position.X, 0f, position.Z);
    }
  }
}
