using global::Godot;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Godot;
using xpTURN.Klotho.Logging;

namespace Meesles.Avalon {
  public abstract partial class GameNode : Node {
    protected InputCapture Input;
    protected Menu Menu;
    protected Hud Hud;

    protected void InitializeSharedNodes() {
      Input = new InputCapture();
      Menu = GetNode<Menu>("UILayer/Menu");
      Hud = GetNode<Hud>("UILayer/Hud");
    }

    protected void SetupView3D() {
      var cam = GetNodeOrNull<Camera3D>("Camera3D");
      if (cam != null) {
        cam.Environment = new global::Godot.Environment {
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

    protected IKLogger CreateLogger(string filePrefix = "Client")
        => GodotKlothoLogger.CreateDefault(filePrefix: filePrefix, categoryName: "Client");

    protected IDataAssetRegistry LoadAssetRegistry() {
      byte[] bytes = global::Godot.FileAccess.GetFileAsBytes("res://Sim/Data/Assets.bytes");
      if (bytes == null || bytes.Length == 0) {
        var err = global::Godot.FileAccess.GetOpenError();
        throw new System.IO.FileNotFoundException($"res://Sim/Data/Assets.bytes not found (err={err})");
      }
      var assets = DataAssetReader.LoadMixedCollectionFromBytes(bytes);
      IDataAssetRegistryBuilder builder = new DataAssetRegistry();
      builder.RegisterRange(assets);

      byte[] layoutBytes = global::Godot.FileAccess.GetFileAsBytes("res://Sim/Data/MapLayout.bytes");
      if (layoutBytes != null && layoutBytes.Length > 0)
        builder.RegisterRange(DataAssetReader.LoadMixedCollectionFromBytes(layoutBytes));

      return builder.Build();
    }

    public override void _UnhandledInput(InputEvent @event) {
      Input?.HandleUnhandledInput(@event);
    }

    public override void _ExitTree() {
      Input?.Dispose();
    }
  }
}
