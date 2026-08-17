using Godot;

namespace Meesles.Avalon;

public partial class CameraController : Camera3D {
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
  private bool _isCameraLocked;
  private bool _isFocusHeld;
  private float _godmodePitch;
  private float _godmodeYaw;
  private bool _justExitedGodmode;
  private bool _isMousePanning;
  private Vector3 _mousePanAnchor;
  private CameraMode _mode = CameraMode.Free;
  private CameraMode _modeBeforeGodmode = CameraMode.Free;
  private bool _wasInGodmode;
  private float _zoomDistance = DefaultHeight / Mathf.Sin(Mathf.DegToRad(-FollowPitchDegrees));

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

    if (@event.IsAction("focus_player")) {
      if (@event.IsActionPressed("focus_player")) BeginFocusFollow();
      else if (@event.IsActionReleased("focus_player")) EndFocusFollow();
      GetViewport().SetInputAsHandled();
      return;
    }

    if (@event.IsActionPressed("toggle_camera_lock")) {
      ToggleCameraLock();
      GetViewport().SetInputAsHandled();
      return;
    }

    if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Middle } middle) {
      if (middle.Pressed) BeginMousePan(middle.Position);
      else EndMousePan();
      GetViewport().SetInputAsHandled();
      return;
    }

    if (_isMousePanning && @event is InputEventMouseMotion drag) {
      UpdateMousePan(drag.Position);
      GetViewport().SetInputAsHandled();
      return;
    }

    if (@event is InputEventMouseButton { Pressed: true } mb) {
      var prevZoom = _zoomDistance;
      if (mb.ButtonIndex == MouseButton.WheelUp)
        _zoomDistance = Mathf.Clamp(_zoomDistance - ZoomStep, ZoomMin, ZoomMax);
      if (mb.ButtonIndex == MouseButton.WheelDown)
        _zoomDistance = Mathf.Clamp(_zoomDistance + ZoomStep, ZoomMin, ZoomMax);
      if (_mode == CameraMode.Free && _zoomDistance != prevZoom)
        GlobalPosition += GlobalTransform.Basis.Z * (_zoomDistance - prevZoom);
    }
  }

  public override void _Process(double delta) {
    var dt = (float)delta;

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

    if (IsFollowing) {
      SmoothFollow(dt);
      return;
    }

    var freeMove = NormalMoveDirection();
    if (freeMove.LengthSquared() > 0f) {
      _mode = CameraMode.Free;
      MoveFree(freeMove, dt);
      return;
    }

    if (_mode == CameraMode.Follow && IsValidFollowTarget()) SmoothFollow(dt);
    else ProcessFree(dt);
  }

  public Vector3? ScreenToGround(Vector2 screenPos) {
    var origin = ProjectRayOrigin(screenPos);
    var dir = ProjectRayNormal(screenPos);
    if (Mathf.Abs(dir.Y) < 0.0001f) return null;
    var t = -origin.Y / dir.Y;
    if (t < 0f) return null;
    return origin + dir * t;
  }

  // Middle-mouse drag pans the map: the world point grabbed on press stays pinned under the cursor for the
  // whole drag. Anchoring on a world position and re-solving it every motion keeps the pan 1:1 at any zoom
  // and self-corrects, where accumulating pixel deltas would drift as the camera moves under the cursor.
  // Panning drops follow mode, same as a keyboard pan does.
  private void BeginMousePan(Vector2 screenPos) {
    if (IsFollowing) return;

    var ground = ScreenToGround(screenPos);
    if (ground == null) return;

    _isMousePanning = true;
    _mousePanAnchor = ground.Value;
    _mode = CameraMode.Free;
  }

  private void UpdateMousePan(Vector2 screenPos) {
    var ground = ScreenToGround(screenPos);
    if (ground == null) return;

    var delta = _mousePanAnchor - ground.Value;
    delta.Y = 0f;
    GlobalPosition += delta;
  }

  private void EndMousePan() {
    _isMousePanning = false;
  }

  private void ProcessGodmode(float dt) {
    var dir = MovementDirectionXz("move_forward", "move_backward", "move_left", "move_right");
    if (dir.LengthSquared() > 0f) GlobalPosition += dir * GodmodeMoveSpeed * dt;
    if (Input.IsActionPressed("move_up")) GlobalPosition += Vector3.Up * GodmodeVerticalSpeed * dt;
    if (Input.IsActionPressed("move_down")) GlobalPosition -= Vector3.Up * GodmodeVerticalSpeed * dt;
  }

  private void ProcessFree(float dt) {
    _mode = CameraMode.Free;
    var dir = NormalMoveDirection();
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

    var desired = CameraPosForTarget(_followTarget.GlobalPosition);
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
    var isInGodmode = _mode == CameraMode.Godmode;
    if (isInGodmode && !_wasInGodmode) OnEnterGodmode();
    else if (!isInGodmode && _wasInGodmode) OnExitGodmode();
    _wasInGodmode = isInGodmode;
  }

  // Space held and the Y lock drive follow directly instead of going through _mode, so releasing either
  // leaves the camera in whatever mode it was in beforehand rather than stomping it to Free.
  private bool IsFollowing => (_isCameraLocked || _isFocusHeld) && IsValidFollowTarget();

  // Tap centers on the player and leaves the camera there; hold keeps it centered until release. One
  // path for both - a tap just ends the follow before the target has moved off the snap.
  private void BeginFocusFollow() {
    _isFocusHeld = true;
    EndMousePan();
    SnapToTarget();
  }

  private void EndFocusFollow() {
    _isFocusHeld = false;
  }

  // The lock outranks panning while it's on: arrow keys and middle-mouse drag are ignored rather than
  // fighting the follow every frame. Kept across a dead target so the camera re-locks on respawn.
  private void ToggleCameraLock() {
    _isCameraLocked = !_isCameraLocked;
    if (_isCameraLocked) {
      EndMousePan();
      SnapToTarget();
      return;
    }

    if (_mode == CameraMode.Follow) _mode = CameraMode.Free;
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
    _isMousePanning = false;
    _isFocusHeld = false;
    Input.MouseMode = Input.MouseModeEnum.Captured;
    SyncGodmodeFromTransform();
  }

  private void OnExitGodmode() {
    _justExitedGodmode = true;
    Input.MouseMode = Input.MouseModeEnum.Visible;
    GlobalTransform = new Transform3D(FollowBasis(), GlobalPosition);
    if (_mode == CameraMode.Follow || _isCameraLocked) SnapToTarget();
  }

  private Vector3 MovementDirectionXz(string forwardAction, string backwardAction, string leftAction,
    string rightAction) {
    var forward = -GlobalTransform.Basis.Z;
    forward.Y = 0f;
    forward = forward.Normalized();

    var right = GlobalTransform.Basis.X;
    right.Y = 0f;
    right = right.Normalized();

    var dir = Vector3.Zero;
    if (Input.IsActionPressed(forwardAction)) dir += forward;
    if (Input.IsActionPressed(backwardAction)) dir -= forward;
    if (Input.IsActionPressed(leftAction)) dir -= right;
    if (Input.IsActionPressed(rightAction)) dir += right;
    return dir.LengthSquared() > 0f ? dir.Normalized() : dir;
  }

  // Arrow keys, not WASD: Q/W/E/R are the hero's skill hotbar (see InputCapture), so a keyboard pan on
  // W would cast and pan off the same tap. Godmode's flycam keeps WASD+QE — it suppresses the skill keys
  // while it's on, so nothing there is double-bound.
  private Vector3 NormalMoveDirection() {
    return MovementDirectionXz("camera_up", "camera_down", "camera_left", "camera_right");
  }

  public bool IsGodmode => _mode == CameraMode.Godmode;

  private void ApplyGodmodeLook(Vector2 delta) {
    _godmodeYaw -= delta.X * MouseSensitivity;
    _godmodePitch -= delta.Y * MouseSensitivity;
    _godmodePitch = Mathf.Clamp(
      _godmodePitch,
      Mathf.DegToRad(GodmodePitchMinDeg),
      Mathf.DegToRad(GodmodePitchMaxDeg));
    GlobalTransform = new Transform3D(Basis.FromEuler(new Vector3(_godmodePitch, _godmodeYaw, 0f)),
      GlobalPosition);
  }

  private void SyncGodmodeFromTransform() {
    var euler = GlobalTransform.Basis.GetEuler();
    _godmodePitch = euler.X;
    _godmodeYaw = euler.Y;
  }

  private static Basis FollowBasis() {
    return Basis.FromEuler(new Vector3(Mathf.DegToRad(FollowPitchDegrees), Mathf.DegToRad(FollowYawDegrees), 0f));
  }

  private bool IsValidFollowTarget() {
    return _followTarget != null && IsInstanceValid(_followTarget);
  }

  private enum CameraMode {
    Free,
    Follow,
    Godmode
  }
}
