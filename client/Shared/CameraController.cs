using global::Godot;

namespace Meesles.Avalon {
  public partial class CameraController : Camera3D {
    private const float CameraTiltDegrees = 55f;
    private const float CameraFollowHeight = 7.5f;

    [Export] public float FollowSpeed = 5f;
    [Export] public float FollowHeightAtMinZoom = 3.5f;
    [Export] public float GodmodeMouseSensitivity = 0.002f;
    [Export] public float GodmodeMoveSpeed = 25f;
    [Export] public float GodmodePitchMaxDegrees = 89f;
    [Export] public float GodmodePitchMinDegrees = -89f;
    [Export] public float GodmodeVerticalSpeed = 15f;
    [Export] public float PanMoveSpeed = 20f;
    [Export] public float FovMax = 75f;
    [Export] public float FovMin = 20f;
    [Export] public float FovZoomLerpSpeed = 12f;
    [Export] public float FovZoomStep = 5f;
    [Export] public float PitchAtMinFovDegrees = 10f;

    private Node3D _followTarget;
    private float _targetFov = 75f;
    private float _zoomT = 1f;
    private float _godmodePitch;
    private float _godmodeYaw;
    private bool _godmodeEnabled;
    private bool _wasGodmode;
    private bool _justExitedGodmode;

    public void SetFollowTarget(Node3D target) {
      _followTarget = target;
      _justExitedGodmode = false;
      if (_followTarget != null) SnapToFollowTarget();
    }

    public override void _Ready() {
      InitializeFov();
      Position = new Vector3(Position.X, CameraFollowHeight, Position.Z);
      GlobalTransform = new Transform3D(GetFollowBasisWithPitch(Mathf.DegToRad(-CameraTiltDegrees)), GlobalPosition);
      SyncGodmodeRotationFromTransform();
    }

    public override void _Input(InputEvent @event) {
      if (@event is InputEventKey keyEvent && keyEvent.IsActionPressed("toggle_godmode") && !keyEvent.Echo) {
        _godmodeEnabled = !_godmodeEnabled;
        GetViewport().SetInputAsHandled();
        return;
      }

      if (_godmodeEnabled) {
        if (@event is InputEventMouseMotion motion) ApplyGodmodeLook(motion.Relative);
        return;
      }

      if (@event is InputEventMouseButton { Pressed: true } mouseButton) {
        if (mouseButton.ButtonIndex == MouseButton.WheelUp) AdjustTargetFov(-FovZoomStep);
        else if (mouseButton.ButtonIndex == MouseButton.WheelDown) AdjustTargetFov(FovZoomStep);
      }
    }

    public override void _Process(double delta) {
      float dt = (float)delta;
      _zoomT = Mathf.Lerp(_zoomT, GetZoomTFromFov(_targetFov), FovZoomLerpSpeed * dt);
      _zoomT = Mathf.Clamp(_zoomT, 0f, 1f);
      Fov = Mathf.Lerp(FovMin, FovMax, _zoomT);

      UpdateMouseCapture();
      if (_godmodeEnabled) {
        _justExitedGodmode = false;
        ProcessGodmode(dt);
        return;
      }

      if (_justExitedGodmode) return;

      UpdateFollowBasisFromZoom();
      if (_followTarget != null) SmoothFollow(dt);
      else ProcessPanNoTarget(dt);
    }

    public Vector3? ScreenToGround(Vector2 screenPosition) {
      Vector3 origin = ProjectRayOrigin(screenPosition);
      Vector3 dir = ProjectRayNormal(screenPosition);
      if (Mathf.Abs(dir.Y) < 0.0001f) return null;
      float t = -origin.Y / dir.Y;
      if (t < 0f) return null;
      return origin + dir * t;
    }

    private void ProcessGodmode(float dt) {
      Vector3 dir = GetWasdDirectionXz();
      if (dir.LengthSquared() > 0f) GlobalPosition += dir * GodmodeMoveSpeed * dt;
      if (Input.IsActionPressed("move_up")) GlobalPosition += Vector3.Up * GodmodeVerticalSpeed * dt;
      if (Input.IsActionPressed("move_down")) GlobalPosition -= Vector3.Up * GodmodeVerticalSpeed * dt;
    }

    private void ProcessPanNoTarget(float dt) {
      Vector3 dir = GetWasdDirectionXz();
      if (dir.LengthSquared() > 0f) GlobalPosition += dir * PanMoveSpeed * dt;
    }

    private void SmoothFollow(float dt) {
      Vector3 viewDir = -GlobalTransform.Basis.Z;
      Vector3 targetPos = _followTarget.GlobalPosition;
      float followHeight = GetFollowHeight();
      float dy = targetPos.Y - followHeight;
      Vector3 desiredPos;

      if (Mathf.Abs(viewDir.Y) < 0.0001f) {
        desiredPos = new Vector3(targetPos.X, followHeight, targetPos.Z);
      }
      else {
        float t = dy / viewDir.Y;
        desiredPos = targetPos - t * viewDir;
        desiredPos.Y = followHeight;
      }

      float lerpT = Mathf.Clamp(FollowSpeed * dt, 0f, 1f);
      GlobalPosition = new Vector3(
          Mathf.Lerp(GlobalPosition.X, desiredPos.X, lerpT),
          followHeight,
          Mathf.Lerp(GlobalPosition.Z, desiredPos.Z, lerpT));
    }

    private void SnapToFollowTarget() {
      Vector3 viewDir = -GlobalTransform.Basis.Z;
      Vector3 targetPos = _followTarget.GlobalPosition;
      float followHeight = GetFollowHeight();
      float dy = targetPos.Y - followHeight;

      if (Mathf.Abs(viewDir.Y) < 0.0001f) {
        GlobalPosition = new Vector3(targetPos.X, followHeight, targetPos.Z);
        return;
      }

      float t = dy / viewDir.Y;
      Vector3 desiredPos = targetPos - t * viewDir;
      desiredPos.Y = followHeight;
      GlobalPosition = desiredPos;
    }

    private void UpdateMouseCapture() {
      if (_godmodeEnabled && !_wasGodmode) OnEnterGodmode();
      else if (!_godmodeEnabled && _wasGodmode) OnExitGodmode();

      _wasGodmode = _godmodeEnabled;
    }

    private void OnEnterGodmode() {
      Input.MouseMode = Input.MouseModeEnum.Captured;
      SyncGodmodeRotationFromTransform();
    }

    private void OnExitGodmode() {
      _justExitedGodmode = true;
      Input.MouseMode = Input.MouseModeEnum.Visible;
      GlobalTransform = new Transform3D(GetFollowBasisWithPitch(GetFollowPitchRadFromFov(Fov)), GlobalPosition);
      if (_followTarget != null) SnapToFollowTarget();
    }

    private Vector3 GetWasdDirectionXz() {
      Vector3 forwardXz = GlobalTransform.Basis.Z;
      forwardXz.Y = 0f;
      forwardXz = forwardXz.Normalized();

      Vector3 right = GlobalTransform.Basis.X;
      right.Y = 0f;
      right = right.Normalized();

      Vector3 dir = Vector3.Zero;
      if (Input.IsActionPressed("move_forward")) dir -= forwardXz;
      if (Input.IsActionPressed("move_backward")) dir += forwardXz;
      if (Input.IsActionPressed("move_left")) dir -= right;
      if (Input.IsActionPressed("move_right")) dir += right;
      return dir.LengthSquared() > 0f ? dir.Normalized() : dir;
    }

    private void ApplyGodmodeLook(Vector2 relative) {
      _godmodeYaw -= relative.X * GodmodeMouseSensitivity;
      _godmodePitch -= relative.Y * GodmodeMouseSensitivity;
      _godmodePitch = Mathf.Clamp(
          _godmodePitch,
          Mathf.DegToRad(GodmodePitchMinDegrees),
          Mathf.DegToRad(GodmodePitchMaxDegrees));
      GlobalTransform = new Transform3D(Basis.FromEuler(new Vector3(_godmodePitch, _godmodeYaw, 0f), EulerOrder.Yxz), GlobalPosition);
    }

    private void SyncGodmodeRotationFromTransform() {
      Vector3 euler = GlobalTransform.Basis.GetEuler(EulerOrder.Yxz);
      _godmodePitch = euler.X;
      _godmodeYaw = euler.Y;
    }

    private void AdjustTargetFov(float step) {
      _targetFov = Mathf.Clamp(_targetFov + step, FovMin, FovMax);
    }

    private void InitializeFov() {
      Fov = Mathf.Clamp(Fov, FovMin, FovMax);
      _targetFov = Fov;
      _zoomT = GetZoomTFromFov(Fov);
    }

    private void UpdateFollowBasisFromZoom() {
      GlobalTransform = new Transform3D(GetFollowBasisWithPitch(GetFollowPitchRadFromZoomT(_zoomT)), GlobalPosition);
    }

    private float GetFollowHeight() {
      return Mathf.Lerp(FollowHeightAtMinZoom, CameraFollowHeight, _zoomT);
    }

    private float GetZoomTFromFov(float fovValue) {
      return Mathf.Clamp((fovValue - FovMin) / (FovMax - FovMin), 0f, 1f);
    }

    private float GetFollowPitchRadFromZoomT(float t) {
      t = Mathf.Clamp(t, 0f, 1f);
      float pitchDeg = Mathf.Lerp(-PitchAtMinFovDegrees, -CameraTiltDegrees, t);
      return Mathf.DegToRad(pitchDeg);
    }

    private float GetFollowPitchRadFromFov(float fovValue) {
      return GetFollowPitchRadFromZoomT(GetZoomTFromFov(fovValue));
    }

    private static Basis GetFollowBasisWithPitch(float pitchRad) {
      return Basis.FromEuler(new Vector3(pitchRad, Mathf.DegToRad(90f), 0f), EulerOrder.Yxz);
    }
  }
}
