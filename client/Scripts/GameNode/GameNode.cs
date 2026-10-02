using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Godot;
using Meesles.Avalon.Sim;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Navigation;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Godot;
using xpTURN.Klotho.Logging;
using FileAccess = Godot.FileAccess;

namespace Meesles.Avalon;

public abstract partial class GameNode : Node {
  protected DebugConsole DebugConsole;
  protected GameUI GameUi;
  protected InputCapture Input;
  protected LobbyUI LobbyUi;
  protected IKLoggerFactory LoggerFactory;

  // Skip entities whose view scene has an invalid root.
  protected readonly HashSet<PackedScene> BrokenViewScenes = [];

  protected void InitializeSharedNodes() {
    Input = new InputCapture();
    LobbyUi = GetNode<LobbyUI>("UILayer/LobbyUI");
  }

  protected void InitializeGameUI() {
    Input = new InputCapture();
    DebugConsole = GetNodeOrNull<DebugConsole>("DebugConsole");
    GameUi = GetNode<GameUI>("GameUI");
    GameUi.ReturnToLobbyRequested = ReturnToLobby;
    Input.BindGameUI(GameUi);
    Input.BindClickMarker(GetNodeOrNull<Node3D>("Crosshair"));
  }

  // Handoff drivers can outlive this scene.
  protected void ReturnToLobby() {
    StopSessionForSceneExit();
    GetTree().ChangeSceneToFile(Scenes.Lobby);
  }

  protected virtual void StopSessionForSceneExit() { }

  // Validate scene roots before adding them to the pool.
  protected bool TryPrewarm(IGodotEntityViewPool pool, PackedScene scene, int count, string label) {
    if (scene == null) {
      LogViewSceneError($"[View] {label}: scene failed to load (null) — check the resource path.");
      return false;
    }

    var probe = scene.Instantiate();
    var isViewNode = probe is EntityViewNode;
    probe.Free();

    if (!isViewNode) {
      BrokenViewScenes.Add(scene);
      LogViewSceneError(
        $"[View] {label}: '{scene.ResourcePath}' root is not an EntityViewNode — attach the " +
        "HeroEntity/MinionEntity script to the scene root. Its units will not render this session.");
      return false;
    }

    pool.Prewarm(scene, count);
    return true;
  }

  private static void LogViewSceneError(string message) {
    GD.PushError(message);
    GD.PrintErr(message);
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

  protected static GameTypeCatalog.GameTypeDef GameType => GameTypeCatalog.Selected;

  protected IDataAssetRegistry LoadAssetRegistry(string mapLayoutPath = null) {
    mapLayoutPath ??= GameType.MapLayoutPath;

    var assets = DataAssetReader.LoadMixedCollectionFromBytes(
      LoadRequiredBytes("res://Sim/Data/Assets.bytes"));
    IDataAssetRegistryBuilder builder = new DataAssetRegistry();
    builder.RegisterRange(assets);

    var layoutAssets = DataAssetReader.LoadMixedCollectionFromBytes(LoadRequiredBytes(mapLayoutPath));
    builder.RegisterRange(layoutAssets);
    GD.Print($"[GameNode] {mapLayoutPath} loaded: {layoutAssets.Count} asset(s)");

    return builder.Build();
  }

  protected byte[] LoadNavigationMeshBytes(string navMeshPath = null) {
    navMeshPath ??= GameType.NavMeshPath;
    return LoadRequiredBytes(navMeshPath);
  }

  private static byte[] LoadRequiredBytes(string path) {
    var bytes = FileAccess.GetFileAsBytes(path);
    if (bytes == null || bytes.Length == 0) {
      var err = FileAccess.GetOpenError();
      throw new FileNotFoundException($"{path} not found (err={err})");
    }

    return bytes;
  }

  protected Node InstantiateWorld() {
    var scene = GD.Load<PackedScene>(GameType.WorldScenePath);
    if (scene == null) {
      GD.PushError($"[GameNode] World scene not found: {GameType.WorldScenePath}");
      return null;
    }

    var world = scene.Instantiate<Node>();
    world.Name = "World";
    AddChild(world);
    MoveChild(world, 0);
    return world;
  }

  protected void BindDebugConsole(IKlothoEngine engine, CameraController camera) {
    DebugConsole?.Bind(Input, engine, camera);
  }

  // Input needs its own read-only navmesh query.
  protected void BindNavigationToInput() {
    var navMesh = FPNavMeshSerializer.Deserialize(LoadNavigationMeshBytes());
    Input.BindNavigation(navMesh, new FPNavMeshQuery(navMesh, null));
  }

  // Remove authored props for teams pruned by the sim.
  protected void BindTeamBaseCleanup(SimEventHub events) {
    events.OnConfirmed<TeamPrunedEvent>(evt => FreeTeamBase(evt.TeamId));
  }

  private void FreeTeamBase(int teamId) {
    GetNodeOrNull($"World/NavigationRegion3D/Team{teamId}")?.QueueFree();
  }

  public override void _Input(InputEvent @event) {
    if (DebugConsole != null && DebugConsole.HandleInput(@event))
      return;

    Input?.HandleUnhandledInput(@event);
  }

  public override void _ExitTree() {
    Input?.Dispose();
    DisposeLoggerFactory();
  }
}
