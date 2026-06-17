using global::Godot;

namespace Meesles.Avalon
{
  public partial class GameNode : Node
  {
    private static readonly Vector3 SpawnPosition = new(0f, 0.5f, 0f);
    private const float MoveSpeed = 5f;
    private const float FallThresholdY = -2f;

    private InputCapture _input;
    private Menu _menu;
    private Hud _hud;
    private Node3D _player;

    public override void _Ready()
    {
      _input = new InputCapture();
      _menu = GetNode<Menu>("UILayer/Menu");
      _hud = GetNode<Hud>("UILayer/Hud");
      _menu.SetSingleplayerMode();
      _menu.OnResetClicked += ResetSandbox;
      _hud.SetSandboxMode();

      SetupView3D();
      EnsurePlayer();
      ResetSandbox();
    }

    public override void _Process(double delta)
    {
      if (_player == null) return;

      _input.CaptureInput();
      var movement = new Vector3(_input.Horizontal.ToFloat(), 0f, _input.Vertical.ToFloat());
      if (movement.LengthSquared() > 1f) movement = movement.Normalized();

      _player.Position += movement * MoveSpeed * (float)delta;

      if (movement.LengthSquared() > 0.001f)
        _player.Rotation = new Vector3(0f, Mathf.Atan2(movement.X, movement.Z), 0f);

      if (_player.Position.Y < FallThresholdY)
      {
        ResetSandbox();
        return;
      }

      _hud.SyncSandbox(_player.Position, movement);
    }

    private void EnsurePlayer()
    {
      if (_player != null) return;

      var playerScene = GD.Load<PackedScene>("res://Shared/Player.tscn");
      _player = playerScene.Instantiate<Node3D>();
      AddChild(_player);

      var mesh = _player.GetNodeOrNull<MeshInstance3D>("Mesh");
      if (mesh != null)
      {
        mesh.MaterialOverride = new StandardMaterial3D
        {
          AlbedoColor = new Color(0.28f, 0.55f, 0.95f),
        };
      }
    }

    private void ResetSandbox()
    {
      EnsurePlayer();
      _player.Position = SpawnPosition;
      _player.Rotation = Vector3.Zero;
      _hud.ShowStatus("Local play only");
      _hud.SyncSandbox(_player.Position, Vector3.Zero);
    }

    private void SetupView3D()
    {
      var cam = GetNodeOrNull<Camera3D>("Camera3D");
      if (cam != null)
      {
        cam.LookAtFromPosition(new Vector3(0, 7, 0), Vector3.Zero, new Vector3(0, 0, -1));
        cam.Environment = new global::Godot.Environment
        {
          BackgroundMode = global::Godot.Environment.BGMode.Color,
          BackgroundColor = new Color(0.12f, 0.13f, 0.18f),
          AmbientLightSource = global::Godot.Environment.AmbientSource.Color,
          AmbientLightColor = new Color(0.5f, 0.5f, 0.5f),
          AmbientLightEnergy = 1.0f,
        };
      }

      var light = GetNodeOrNull<DirectionalLight3D>("DirectionalLight3D");
      light?.LookAtFromPosition(new Vector3(4, 10, 4), Vector3.Zero, Vector3.Up);
    }

    public override void _ExitTree()
    {
      _input?.Dispose();
    }
  }
}
