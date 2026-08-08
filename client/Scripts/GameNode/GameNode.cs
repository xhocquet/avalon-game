using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Godot;
using Meesles.Avalon.Sim;
using Meesles.Avalon.Sim.Navigation;
using xpTURN.Klotho.Deterministic.Navigation;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Godot;
using xpTURN.Klotho.Logging;
using FileAccess = Godot.FileAccess;

namespace Meesles.Avalon;

public abstract partial class GameNode : Node {
  protected const string LobbyScenePath = "res://Scenes/Lobby.tscn";

  protected GameUI GameUi;
  protected InputCapture Input;
  protected LobbyUI LobbyUi;
  protected IKLoggerFactory LoggerFactory;

  // Scenes that failed the TryPrewarm probe. UnitViewFactory resolves these to null so the entities
  // using them are skipped instead of throwing again on the first Rent.
  protected readonly HashSet<PackedScene> BrokenViewScenes = [];

  protected void InitializeSharedNodes() {
    Input = new InputCapture();
    LobbyUi = GetNode<LobbyUI>("UILayer/LobbyUI");
  }

  protected void InitializeGameUI() {
    Input = new InputCapture();
    GameUi = GetNode<GameUI>("GameUI");
    GameUi.ReturnToLobbyRequested = ReturnToLobby;
    Input.BindGameUI(GameUi);
    Input.BindClickMarker(GetNodeOrNull<Node3D>("Crosshair"));
  }

  // The session and its driver can outlive this scene - the lobby hands both over and parents the
  // driver to the tree root - so leaving has to stop them before the swap, or the old driver keeps
  // ticking a dead session underneath the fresh lobby.
  protected void ReturnToLobby() {
    StopSessionForSceneExit();
    GetTree().ChangeSceneToFile(LobbyScenePath);
  }

  // Nothing by default: a node whose driver is its own child is already torn down by _ExitTree.
  protected virtual void StopSessionForSceneExit() { }


  // PackedScene.Instantiate<EntityViewNode> throws when the scene root carries no EntityViewNode
  // script (a model .tscn wired up without one). Unguarded that aborts _Ready mid-way, so the client
  // comes up with no camera, no input binding and no units at all. Probe the root first: a mis-wired
  // scene then costs only the entities that use it.
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
    GD.PushError(message);   // editor Errors dock + stderr
    GD.PrintErr(message);    // headless/smoke stderr, where PushError alone is easy to miss
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

  // Gives InputCapture its own read-only navmesh query so right-click move targets can be snapped
  // onto walkable ground (structures carve holes the raw click lands inside). Deserializes a fresh
  // navmesh from the baked bytes rather than reaching into the sim's private NavigationRuntime; the
  // query only reads. Logger is optional (KDebug traces only), so null is fine here.
  protected void BindNavigationToInput() {
    var navMesh = FPNavMeshSerializer.Deserialize(LoadNavigationMeshBytes());
    Input.BindNavigation(navMesh, new FPNavMeshQuery(navMesh, null));
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
