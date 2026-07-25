// Avalon multiplayer game scene. Lobby readiness lives in LobbyGameNode; this scene renders play.

using System.Threading.Tasks;
using Godot;
using Meesles.Avalon.Client;
using Meesles.Avalon.Client.Scripts;
using Meesles.Avalon.Client.Scripts.View;
using Meesles.Avalon.Sim.Models;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Godot;
using xpTURN.Klotho.LiteNetLib;
using xpTURN.Klotho.Logging;
using xpTURN.Klotho.Network;

namespace Meesles.Avalon;

public partial class MultiplayerGameNode : GameNode {
  private const string ConnectionKey = "Meesles.Avalon";
  private const int RoomId = 0;
  private const int VerifyTick = 120;
  private bool _autoReadySent;
  private CameraController _camera;
  private GodotSessionDriver _driver;
  private SimEventHub _events;
  private FactionCatalog _factions;
  private KlothoSessionFlow _flow;
  private bool _joining;
  private Task<KlothoSession> _joinTask;
  private bool _localViewFocused;

  private IKLogger _logger;
  private bool _ownsDriver;
  private DefaultGodotEntityViewPool _pool;
  private IDataAssetRegistry _registry;
  private ISessionConfig _sesCfg;
  private KlothoSession _session;
  private ISimulationConfig _simCfg;
  private SimCallbacks _simulationCallbacks;
  private LiteNetLibTransport _transport;
  private bool _verified;
  private VfxManager _vfx;
  private EntityViewUpdaterNode _view;
  private ViewCallbacks _viewCallbacks;

  public override void _Ready() {
    WarmupRegistry.RunAll();

    InitializeGameUI();
    GameUi.SetMultiplayerMode();

    _camera = GetNodeOrNull<CameraController>("Camera3D");
    Input.BindCamera(_camera);
    BindNavigationToInput();

    CreateView();

    var handoff = MultiplayerSessionHandoff.Consume();
    if (handoff != null)
      AdoptHandoff(handoff);
    else
      StartDirectJoinFallback();
  }

  private void AdoptHandoff(MultiplayerSessionHandoff handoff) {
    _logger = handoff.Logger;
    LoggerFactory = handoff.LoggerFactory;
    _transport = handoff.Transport;
    _flow = handoff.Flow;
    _session = handoff.Session;
    _simulationCallbacks = handoff.SimulationCallbacks;
    _viewCallbacks = handoff.ViewCallbacks;
    _driver = handoff.Driver;
    _simCfg = handoff.SimulationConfig;
    _sesCfg = handoff.SessionConfig;

    _simulationCallbacks.SetInput(Input);
    _viewCallbacks.SetHud(GameUi);
    _driver.PreSessionUpdate += CaptureRunningInput;
    OnSessionReady(false);
  }

  private void StartDirectJoinFallback() {
    _logger = CreateLogger();
    _registry = LoadAssetRegistry();
    var navMeshBytes = LoadNavigationMeshBytes();
    _simCfg = new SimulationConfig { Mode = NetworkMode.ServerDriven };
    _sesCfg = new SessionConfig { MaxPlayers = 2, MinPlayers = 2, CountdownDurationMs = 0 };
    _transport = new LiteNetLibTransport(_logger, connectionKey: ConnectionKey);
    _simulationCallbacks = new SimCallbacks(Input, navMeshBytes, _logger);
    _viewCallbacks = new ViewCallbacks(GameUi);

    _flow = new KlothoSessionFlow(
      new KlothoFlowSetupBuilder((s, ss) =>
          new SessionCallbacks(_simulationCallbacks, _viewCallbacks))
        .WithLogger(_logger)
        .WithTransport(_transport)
        .WithAssetRegistry(_registry)
        .WithGodotDefaults()
        .Build()
    );

    _driver = new GodotSessionDriver();
    _ownsDriver = true;
    AddChild(_driver);
    _driver.BindTransport(_transport);
    _driver.PreSessionUpdate += CaptureRunningInput;

    _joining = true;
    _joinTask = _flow.JoinServerDrivenAsync(
      _transport,
      "127.0.0.1",
      7777,
      RoomId,
      _sesCfg,
      _driver.TrackConnection);
  }

  private void CreateView() {
    _pool = new DefaultGodotEntityViewPool();
    _factions = FactionCatalog.CreateDefault();
    Input.BindFactionCatalog(_factions);
    var crystalScene = GD.Load<PackedScene>("res://Scenes/Objects/Crystal.tscn");
    var turretScene = GD.Load<PackedScene>("res://Scenes/Objects/Turret.tscn");
    var pickupScene = GD.Load<PackedScene>("res://Scenes/Objects/WaterBottle.tscn");
    var oasisScene = GD.Load<PackedScene>("res://Scenes/Objects/Oasis.tscn");
    foreach (var faction in _factions.Entries) {
      _pool.Prewarm(faction.HeroScene, 2);
      _pool.Prewarm(faction.MinionScene, 64);
    }

    _pool.Prewarm(crystalScene, 2);
    _pool.Prewarm(turretScene, 4);
    _pool.Prewarm(pickupScene, 32);
    _pool.Prewarm(oasisScene, 4);

    _view = new EntityViewUpdaterNode();
    AddChild(_view);
    Input.BindViewRoot(_view);
  }

  private UnitViewFactory CreateFactory() {
    var crystalScene = GD.Load<PackedScene>("res://Scenes/Objects/Crystal.tscn");
    var turretScene = GD.Load<PackedScene>("res://Scenes/Objects/Turret.tscn");
    var pickupScene = GD.Load<PackedScene>("res://Scenes/Objects/WaterBottle.tscn");
    var oasisScene = GD.Load<PackedScene>("res://Scenes/Objects/Oasis.tscn");
    return new UnitViewFactory(_factions, crystalScene, turretScene, pickupScene, oasisScene);
  }

  private void OnSessionReady(bool autoReady) {
    _view.Initialize(_session.Engine, CreateFactory(), _pool);
    _view.PlayerViews.OnLocalViewRegistered += OnLocalViewRegistered;
    _view.PlayerViews.OnLocalViewUnregistered += OnLocalViewUnregistered;
    _events = new SimEventHub();
    _events.Attach(_session.Engine);
    BindTeamBaseCleanup(_events);
    _vfx = new VfxManager();
    _vfx.Attach(_events, _view);
    GameUi.BindSimEvents(_events);
    GameUi.SetPhase(_session.Phase);
    TryFocusRegisteredLocalView();

    if (autoReady)
      SendReady();
  }

  private void SendReady() {
    if (_session == null || _autoReadySent) return;
    GameUi.SetLocalReady(true);
    _session.SetReady(true);
    _autoReadySent = true;
    _logger?.KInformation($"[Client] auto-ready sent from multiplayer scene.");
  }

  private void CaptureRunningInput(KlothoSession session, float dt) {
    if (session.State == KlothoState.Running)
      Input.CaptureInput();
  }

  private void OnLocalViewRegistered(EntityViewNode view) {
    _localViewFocused = true;
    _camera?.SetFollowTarget(view);
    var frame = view.Engine?.PredictedFrame.Frame;
    if (frame != null && frame.Has<Team>(view.EntityRef))
      Input.SetLocalTeamId(frame.GetReadOnly<Team>(view.EntityRef).TeamId);
    Input.SelectSingleView(view);
  }

  private void OnLocalViewUnregistered(EntityViewNode view) {
    _localViewFocused = false;
    _camera?.SetFollowTarget(null);
  }

  private void TryFocusRegisteredLocalView() {
    if (_localViewFocused || _session?.Engine == null || _view?.PlayerViews == null)
      return;

    var localPlayerId = _session.Engine.LocalPlayerId;
    if (localPlayerId < 0)
      return;

    var localView = _view.PlayerViews.Get(localPlayerId);
    localView ??= FindLocalPlayerView(localPlayerId);
    if (localView != null)
      OnLocalViewRegistered(localView);
  }

  private EntityViewNode FindLocalPlayerView(int localPlayerId) {
    if (_view == null)
      return null;

    foreach (var child in _view.GetChildren()) {
      if (child is not EntityViewNode view)
        continue;
      if (view is not IPlayerView)
        continue;
      if (view.OwnerMatches(localPlayerId))
        return view;
    }

    return null;
  }

  private void UnbindCameraFollow() {
    if (_view?.PlayerViews != null) {
      _view.PlayerViews.OnLocalViewRegistered -= OnLocalViewRegistered;
      _view.PlayerViews.OnLocalViewUnregistered -= OnLocalViewUnregistered;
    }

    _camera?.SetFollowTarget(null);
  }

  public override void _Process(double delta) {
    if (_joining && _joinTask != null) {
      if (_joinTask.IsFaulted) {
        _logger.KError($"[Client] join failed (server running?): {_joinTask.Exception?.GetBaseException().Message}");
        _joining = false;
        _joinTask = null;
        if (DisplayServer.GetName() == "headless") GetTree().Quit(1);
      }
      else if (_joinTask.IsCompleted) {
        _session = _joinTask.Result;
        _joining = false;
        _joinTask = null;
        OnSessionReady(true);
      }
    }

    if (_session == null) return;

    GameUi.SetPhase(_session.Phase);
    if (!_autoReadySent && _session.Phase == SessionPhase.Synchronized)
      SendReady();

    TryFocusRegisteredLocalView();
    AutoTestStep();
  }

  private void AutoTestStep() {
    if (_session == null) return;
    if (!_verified && _session.State == KlothoState.Running && _session.Engine.CurrentTick >= VerifyTick) {
      _verified = true;
      var n = _view.GetChildCount();
      _logger.KInformation($"[Client] auto-join tick={_session.Engine.CurrentTick} viewNodes={n}");
      if (n >= 1) _logger.KInformation($"=== CLIENT OK ===");
      else _logger.KError($"=== CLIENT FAILED (viewNodes={n}) ===");
      if (DisplayServer.GetName() == "headless") GetTree().Quit(n >= 1 ? 0 : 1);
    }
  }

  public override void _ExitTree() {
    UnbindCameraFollow();
    if (_driver != null)
      _driver.PreSessionUpdate -= CaptureRunningInput;
    if (_ownsDriver && _session != null) {
      _driver?.DetachAndStop();
      _session = null;
    }

    _vfx?.Detach();
    _events?.Detach();
    _view?.Cleanup();
    _viewCallbacks?.Cleanup();
    _pool?.Dispose();
    base._ExitTree();
  }
}
