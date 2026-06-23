using Godot;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Godot;
using xpTURN.Klotho.Logging;

namespace Meesles.Avalon {
  public abstract partial class GameNode : Node {
    protected InputCapture Input;
    protected Menu Menu;
    protected LobbyUI LobbyUi;
    protected GameUI GameUi;
    protected IKLoggerFactory LoggerFactory;

    protected void InitializeSharedNodes() {
      Input = new InputCapture();
      Menu = GetNode<Menu>("UILayer/Menu");
      LobbyUi = GetNode<LobbyUI>("UILayer/LobbyUI");
    }

    protected void InitializeGameUI() {
      Input = new InputCapture();
      GameUi = GetNode<GameUI>("GameUI");
      Input.BindGameUI(GameUi);
    }

    protected void SetupView3D() {
      var cam = GetNodeOrNull<Camera3D>("Camera3D");
      if (cam != null) {
        cam.Environment = new Environment {
          BackgroundMode = Environment.BGMode.Color,
          BackgroundColor = new Color(0.12f, 0.13f, 0.18f),
          AmbientLightSource = Environment.AmbientSource.Color,
          AmbientLightColor = new Color(0.5f, 0.5f, 0.5f),
          AmbientLightEnergy = 1.0f,
        };
      }

      var light = GetNodeOrNull<DirectionalLight3D>("DirectionalLight3D");
      light?.LookAtFromPosition(new Vector3(4, 10, 4), Vector3.Zero, Vector3.Up);
    }

    protected IKLogger CreateLogger(string filePrefix = "Client") {
      DisposeLoggerFactory();

      var logDir = ProjectSettings.GlobalizePath("user://logs");
      System.IO.Directory.CreateDirectory(logDir);
      var uniquePrefix = $"{filePrefix}_{System.Diagnostics.Process.GetCurrentProcess().Id}_{Time.GetTicksMsec()}";

      LoggerFactory = KLoggerFactory.Create(builder => {
        builder.SetMinimumLevel(KLogLevel.Information);
        builder.AddSink(new GodotLogSink());
        builder.AddRollingFile(options => {
          options.FilePrefix = uniquePrefix;
          options.Directory = logDir;
        });
      });

      return LoggerFactory.CreateLogger("Client");
    }

    protected void DisposeLoggerFactory() {
      LoggerFactory?.Dispose();
      LoggerFactory = null;
    }

    protected IDataAssetRegistry LoadAssetRegistry() {
      byte[] bytes = FileAccess.GetFileAsBytes("res://Sim/Data/Assets.bytes");
      if (bytes == null || bytes.Length == 0) {
        var err = FileAccess.GetOpenError();
        throw new System.IO.FileNotFoundException($"res://Sim/Data/Assets.bytes not found (err={err})");
      }

      var assets = DataAssetReader.LoadMixedCollectionFromBytes(bytes);
      IDataAssetRegistryBuilder builder = new DataAssetRegistry();
      builder.RegisterRange(assets);

      byte[] layoutBytes = FileAccess.GetFileAsBytes("res://Sim/Data/MapLayout.bytes");
      if (layoutBytes != null && layoutBytes.Length > 0) {
        var layoutAssets = DataAssetReader.LoadMixedCollectionFromBytes(layoutBytes);
        builder.RegisterRange(layoutAssets);
        GD.Print($"[GameNode] MapLayout.bytes loaded: {layoutAssets.Count} asset(s)");
      }
      else {
        GD.PrintErr("[GameNode] MapLayout.bytes missing or empty — spawn positions will use hardcoded fallbacks");
      }

      return builder.Build();
    }

    public override void _Input(InputEvent @event) {
      Input?.HandleUnhandledInput(@event);
    }

    public override void _ExitTree() {
      Input?.Dispose();
      DisposeLoggerFactory();
    }
  }
}
