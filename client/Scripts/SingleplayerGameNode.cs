using global::Godot;

namespace Meesles.Avalon {
  public partial class SingleplayerGameNode : GameNode {
    private static readonly Vector3 SpawnPosition = new(0f, 0.5f, 0f);
    private const float MoveSpeed = 5f;
    private const float FallThresholdY = -2f;
    private const float StopDistance = 0.15f;

    private Node3D _player;
    private Node3D _moveTarget;

    public override void _Ready() {
      InitializeSharedNodes();
      Menu.SetSingleplayerMode();
      Menu.OnResetClicked += ResetSandbox;
      Hud.SetSandboxMode();

      SetupView3D();
      EnsurePlayer();
      EnsureMoveTarget();
      var camera = GetNodeOrNull<CameraController>("Camera3D");
      camera?.SetFollowTarget(_player);
      Input.BindCamera(camera);
      Input.BindSingleplayerMoveTarget(_moveTarget);
      ResetSandbox();
    }

    public override void _Process(double delta) {
      if (_player == null) return;

      Input.CaptureInput();
      var movement = Vector3.Zero;
      if (Input.HasSingleplayerTarget) {
        Vector3 toTarget = Input.SingleplayerTarget - _player.GlobalPosition;
        toTarget.Y = 0f;
        if (toTarget.Length() <= StopDistance) {
          Input.ClearSingleplayerTarget();
        }
        else {
          movement = toTarget.Normalized();
          _player.GlobalPosition += movement * MoveSpeed * (float)delta;
        }
      }

      if (movement.LengthSquared() > 0.001f)
        _player.Rotation = new Vector3(0f, Mathf.Atan2(movement.X, movement.Z), 0f);

      if (_player.Position.Y < FallThresholdY) {
        ResetSandbox();
        return;
      }

      Hud.SyncSandbox(_player.Position, movement);
    }

    private void EnsurePlayer() {
      if (_player != null) return;

      var playerScene = GD.Load<PackedScene>("res://Shared/Player.tscn");
      _player = playerScene.Instantiate<Node3D>();
      AddChild(_player);

      var mesh = _player.GetNodeOrNull<MeshInstance3D>("Mesh");
      if (mesh != null) {
        mesh.MaterialOverride = new StandardMaterial3D {
          AlbedoColor = new Color(0.28f, 0.55f, 0.95f),
        };
      }
    }

    private void ResetSandbox() {
      EnsurePlayer();
      _player.Position = SpawnPosition;
      _player.Rotation = Vector3.Zero;
      Input.ClearSingleplayerTarget();
      Hud.ShowStatus("Local play only");
      Hud.SyncSandbox(_player.Position, Vector3.Zero);
    }

    private void EnsureMoveTarget() {
      if (_moveTarget != null) return;
      _moveTarget = new Node3D { Name = "MoveTarget" };
      AddChild(_moveTarget);
    }
  }
}
