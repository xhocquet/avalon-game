using Godot;

namespace Meesles.Avalon {
  public partial class CameraController : Camera3D {
    private enum CameraMode {
      Free,
      Follow,
      Godmode,
    }

    private const float FollowPitchDegrees = -58f;
    private const float FollowYawDegrees = 0f;
    private const float DefaultHeight = 18f;
    private const float DefaultFov = 58f;
    private const float ZoomStep = 2f;
    private const float ZoomMin = 5f;
    private const float ZoomMax = 90f;
    private const float FollowSpeed = 5f;
    private const float PanSpeed = 18f;
    private const float MouseSensitivity = 0.002f;
    private const float GodmodeMoveSpeed = 25f;
    private const float GodmodeVerticalSpeed = 15f;
    private const float GodmodePitchMaxDeg = 89f;
    private const float GodmodePitchMinDeg = -89f;

    private Node3D _followTarget;
    private float _zoomDistance = DefaultHeight / Mathf.Sin(Mathf.DegToRad(-FollowPitchDegrees));
    private float _godmodePitch;
    private float _godmodeYaw;
    private CameraMode _mode = CameraMode.Free;
    private CameraMode _modeBeforeGodmode = CameraMode.Free;
    private bool _wasInGodmode;
    private bool _justExitedGodmode;

    public void SetFollowTarget(Node3D target) {
      _followTarget = target;
      _justExitedGodmode = false;
      if (_followTarget != null) {
        _mode = CameraMode.Follow;
        SnapToTarget();
      }
      else if (_mode == CameraMode.Follow) {
        _mode = CameraMode.Free;
      }
    }

    public override void _Ready() {
      Fov = DefaultFov;
      GlobalTransform = new Transform3D(FollowBasis(), GlobalPosition);
      SyncGodmodeFromTransform();
    }

    public override void _Input(InputEvent @event) {
      if (@event is InputEventKey key && key.IsActionPressed("toggle_godmode") && !key.Echo) {
        ToggleGodmode();
        GetViewport().SetInputAsHandled();
        return;
      }

      if (_mode == CameraMode.Godmode) {
        if (@event is InputEventMouseMotion motion) ApplyGodmodeLook(motion.Relative);
        return;
      }

      if (@event is InputEventMouseButton { Pressed: true } mb) {
        float prevZoom = _zoomDistance;
        if (mb.ButtonIndex == MouseButton.WheelUp)
          _zoomDistance = Mathf.Clamp(_zoomDistance - ZoomStep, ZoomMin, ZoomMax);
        if (mb.ButtonIndex == MouseButton.WheelDown)
          _zoomDistance = Mathf.Clamp(_zoomDistance + ZoomStep, ZoomMin, ZoomMax);
        if (_mode == CameraMode.Free && _zoomDistance != prevZoom)
          GlobalPosition += GlobalTransform.Basis.Z * (_zoomDistance - prevZoom);
      }
    }

    public override void _Process(double delta) {
      float dt = (float)delta;

      UpdateMouseCapture();
      if (_mode == CameraMode.Godmode) {
        _justExitedGodmode = false;
        ProcessGodmode(dt);
        return;
      }

      if (_justExitedGodmode) {
        _justExitedGodmode = false;
        return;
      }

      Vector3 freeMove = NormalMoveDirection();
      if (freeMove.LengthSquared() > 0f) {
        _mode = CameraMode.Free;
        MoveFree(freeMove, dt);
        return;
      }

      if (_mode == CameraMode.Follow && IsValidFollowTarget()) SmoothFollow(dt);
      else ProcessFree(dt);
    }

    public Vector3? ScreenToGround(Vector2 screenPos) {
      Vector3 origin = ProjectRayOrigin(screenPos);
      Vector3 dir = ProjectRayNormal(screenPos);
      if (Mathf.Abs(dir.Y) < 0.0001f) return null;
      float t = -origin.Y / dir.Y;
      if (t < 0f) return null;
      return origin + dir * t;
    }

    private void ProcessGodmode(float dt) {
      Vector3 dir = MovementDirectionXz("move_forward", "move_backward", "move_left", "move_right");
      if (dir.LengthSquared() > 0f) GlobalPosition += dir * GodmodeMoveSpeed * dt;
      if (Input.IsActionPressed("move_up")) GlobalPosition += Vector3.Up * GodmodeVerticalSpeed * dt;
      if (Input.IsActionPressed("move_down")) GlobalPosition -= Vector3.Up * GodmodeVerticalSpeed * dt;
    }

    private void ProcessFree(float dt) {
      _mode = CameraMode.Free;
      Vector3 dir = NormalMoveDirection();
      MoveFree(dir, dt);
    }

    private void MoveFree(Vector3 dir, float dt) {
      if (dir.LengthSquared() > 0f) GlobalPosition += dir * PanSpeed * dt;
    }

    private void SmoothFollow(float dt) {
      if (!IsValidFollowTarget()) {
        _followTarget = null;
        _mode = CameraMode.Free;
        return;
      }

      Vector3 desired = CameraPosForTarget(_followTarget.GlobalPosition);
      GlobalPosition = GlobalPosition.Lerp(desired, Mathf.Clamp(FollowSpeed * dt, 0f, 1f));
    }

    private void SnapToTarget() {
      if (!IsValidFollowTarget()) return;
      GlobalPosition = CameraPosForTarget(_followTarget.GlobalPosition);
    }

    private Vector3 CameraPosForTarget(Vector3 targetPos) {
      return targetPos + GlobalTransform.Basis.Z * _zoomDistance;
    }

    private void UpdateMouseCapture() {
      bool isInGodmode = _mode == CameraMode.Godmode;
      if (isInGodmode && !_wasInGodmode) OnEnterGodmode();
      else if (!isInGodmode && _wasInGodmode) OnExitGodmode();
      _wasInGodmode = isInGodmode;
    }

    private void ToggleGodmode() {
      if (_mode == CameraMode.Godmode) {
        _mode = _modeBeforeGodmode;
        if (_mode == CameraMode.Follow && !IsValidFollowTarget())
          _mode = CameraMode.Free;
        return;
      }

      _modeBeforeGodmode = _mode;
      _mode = CameraMode.Godmode;
    }

    private void OnEnterGodmode() {
      Input.MouseMode = Input.MouseModeEnum.Captured;
      SyncGodmodeFromTransform();
    }

    private void OnExitGodmode() {
      _justExitedGodmode = true;
      Input.MouseMode = Input.MouseModeEnum.Visible;
      GlobalTransform = new Transform3D(FollowBasis(), GlobalPosition);
      if (_mode == CameraMode.Follow) SnapToTarget();
    }

    private Vector3 MovementDirectionXz(string forwardAction, string backwardAction, string leftAction,
      string rightAction) {
      Vector3 forward = -GlobalTransform.Basis.Z;
      forward.Y = 0f;
      forward = forward.Normalized();

      Vector3 right = GlobalTransform.Basis.X;
      right.Y = 0f;
      right = right.Normalized();

      Vector3 dir = Vector3.Zero;
      if (Input.IsActionPressed(forwardAction)) dir += forward;
      if (Input.IsActionPressed(backwardAction)) dir -= forward;
      if (Input.IsActionPressed(leftAction)) dir -= right;
      if (Input.IsActionPressed(rightAction)) dir += right;
      return dir.LengthSquared() > 0f ? dir.Normalized() : dir;
    }

    private Vector3 NormalMoveDirection() {
      return MovementDirectionXz("move_forward", "move_backward", "move_left", "move_right");
    }

    private void ApplyGodmodeLook(Vector2 delta) {
      _godmodeYaw -= delta.X * MouseSensitivity;
      _godmodePitch -= delta.Y * MouseSensitivity;
      _godmodePitch = Mathf.Clamp(
        _godmodePitch,
        Mathf.DegToRad(GodmodePitchMinDeg),
        Mathf.DegToRad(GodmodePitchMaxDeg));
      GlobalTransform = new Transform3D(Basis.FromEuler(new Vector3(_godmodePitch, _godmodeYaw, 0f), EulerOrder.Yxz),
        GlobalPosition);
    }

    private void SyncGodmodeFromTransform() {
      Vector3 euler = GlobalTransform.Basis.GetEuler(EulerOrder.Yxz);
      _godmodePitch = euler.X;
      _godmodeYaw = euler.Y;
    }

    private static Basis FollowBasis() {
      return Basis.FromEuler(new Vector3(Mathf.DegToRad(FollowPitchDegrees), Mathf.DegToRad(FollowYawDegrees), 0f),
        EulerOrder.Yxz);
    }

    private bool IsValidFollowTarget() {
      return _followTarget != null && IsInstanceValid(_followTarget);
    }
  }
}
