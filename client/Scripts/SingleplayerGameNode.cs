using global::Godot;

namespace Meesles.Avalon {
  public partial class SingleplayerGameNode : GameNode {
    private static readonly Vector3 SpawnPosition = new(0f, 0.5f, 0f);
    private const float MoveSpeed = 5f;
    private const float FallThresholdY = -2f;

    private Node3D _player;

    public override void _Ready() {
      InitializeSharedNodes();
      Menu.SetSingleplayerMode();
      Menu.OnResetClicked += ResetSandbox;
      Hud.SetSandboxMode();

      SetupView3D();
      EnsurePlayer();
      GetNodeOrNull<CameraController>("Camera3D")?.SetFollowTarget(_player);
      ResetSandbox();
    }

    public override void _Process(double delta) {
      if (_player == null) return;

      Input.CaptureInput();
      var movement = new Vector3(Input.Horizontal.ToFloat(), 0f, Input.Vertical.ToFloat());
      if (movement.LengthSquared() > 1f) movement = movement.Normalized();

      _player.Position += movement * MoveSpeed * (float)delta;

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
      Hud.ShowStatus("Local play only");
      Hud.SyncSandbox(_player.Position, Vector3.Zero);
    }
  }
}
