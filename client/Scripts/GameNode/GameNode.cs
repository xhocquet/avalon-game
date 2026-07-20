using System.Diagnostics;
using System.IO;
using Godot;
using Meesles.Avalon.Sim;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Godot;
using xpTURN.Klotho.Logging;
using FileAccess = Godot.FileAccess;

namespace Meesles.Avalon;

public abstract partial class GameNode : Node {
  protected GameUI GameUi;
  protected InputCapture Input;
  protected LobbyUI LobbyUi;
  protected IKLoggerFactory LoggerFactory;
  protected Menu Menu;

  protected void InitializeSharedNodes() {
    Input = new InputCapture();
    Menu = GetNode<Menu>("UILayer/Menu");
    LobbyUi = GetNode<LobbyUI>("UILayer/LobbyUI");
  }

  protected void InitializeGameUI() {
    Input = new InputCapture();
    GameUi = GetNode<GameUI>("GameUI");
    Input.BindGameUI(GameUi);
    Input.BindClickMarker(GetNodeOrNull<Node3D>("Crosshair"));
  }


  protected IKLogger CreateLogger(string filePrefix = "Client") {
    DisposeLoggerFactory();

    var logDir = ProjectSettings.GlobalizePath("user://logs");
    Directory.CreateDirectory(logDir);
    var uniquePrefix = $"{filePrefix}_{Process.GetCurrentProcess().Id}_{Time.GetTicksMsec()}";

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
    var bytes = FileAccess.GetFileAsBytes("res://Sim/Data/Assets.bytes");
    if (bytes == null || bytes.Length == 0) {
      var err = FileAccess.GetOpenError();
      throw new FileNotFoundException($"res://Sim/Data/Assets.bytes not found (err={err})");
    }

    var assets = DataAssetReader.LoadMixedCollectionFromBytes(bytes);
    IDataAssetRegistryBuilder builder = new DataAssetRegistry();
    builder.RegisterRange(assets);

    var layoutBytes = FileAccess.GetFileAsBytes("res://Sim/Data/MapLayout.bytes");
    if (layoutBytes == null || layoutBytes.Length == 0) {
      var err = FileAccess.GetOpenError();
      throw new FileNotFoundException($"res://Sim/Data/MapLayout.bytes not found (err={err})");
    }

    var layoutAssets = DataAssetReader.LoadMixedCollectionFromBytes(layoutBytes);
    builder.RegisterRange(layoutAssets);
    GD.Print($"[GameNode] MapLayout.bytes loaded: {layoutAssets.Count} asset(s)");

    return builder.Build();
  }

  protected byte[] LoadNavigationMeshBytes() {
    var bytes = FileAccess.GetFileAsBytes("res://Sim/Data/NavigationRegion3D.NavMeshData.bytes");
    if (bytes == null || bytes.Length == 0) {
      var err = FileAccess.GetOpenError();
      throw new FileNotFoundException($"res://Sim/Data/NavigationRegion3D.NavMeshData.bytes not found (err={err})");
    }

    return bytes;
  }

  // The map authors a base per team; TeamPruneSystem deletes the sim entities of teams no player is
  // on at match setup and raises TeamPrunedEvent per removed team. Free that team's authored props
  // (World.tscn Team{N} — crystal, turrets, spawn, shop) so the static scene matches the live sim.
  // Synced event → fires once on the authoritative prune; QueueFree is safe to miss on a later
  // session restart because GetNodeOrNull returns null once the node is already gone.
  protected void BindTeamBaseCleanup(SimEventHub events) {
    events.OnConfirmed<TeamPrunedEvent>(evt => FreeTeamBase(evt.TeamId));
  }

  private void FreeTeamBase(int teamId) {
    GetNodeOrNull($"World/NavigationRegion3D/Team{teamId}")?.QueueFree();
  }

  public override void _Input(InputEvent @event) {
    Input?.HandleUnhandledInput(@event);
  }

  public override void _ExitTree() {
    Input?.Dispose();
    DisposeLoggerFactory();
  }
}
