// Avalon multiplayer game scene. Lobby readiness lives in LobbyGameNode; this scene renders play.
using System.Threading.Tasks;
using global::Godot;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Godot;
using xpTURN.Klotho.LiteNetLib;
using xpTURN.Klotho.Logging;
using xpTURN.Klotho.Network;

namespace Meesles.Avalon {
  public partial class MultiplayerGameNode : GameNode {
    private const string ConnectionKey = "Meesles.Avalon";
    private const int RoomId = 0;
    private const int VerifyTick = 120;

    [Export] public double StartDelaySeconds { get; set; } = 1.0;

    private IKLogger _logger;
    private IDataAssetRegistry _registry;
    private LiteNetLibTransport _transport;
    private KlothoSessionFlow _flow;
    private KlothoSession _session;
    private SimulationCallbacks _simulationCallbacks;
    private ViewCallbacks _viewCallbacks;
    private EntityViewUpdaterNode _view;
    private DefaultGodotEntityViewPool _pool;
    private GodotSessionDriver _driver;
    private CameraController _camera;
    private ISimulationConfig _simCfg;
    private ISessionConfig _sesCfg;
    private Task<KlothoSession> _joinTask;
    private bool _joining;
    private bool _autoReadySent;
    private bool _verified;
    private bool _ownsDriver;
    private ulong _sceneStartedAtMs;

    public override void _Ready() {
      WarmupRegistry.RunAll();

      InitializeSharedNodes();
      Menu.SetGameMode();
      Hud.SetMultiplayerMode();
      SetupView3D();

      _camera = GetNodeOrNull<CameraController>("Camera3D");
      Input.BindCamera(_camera);
      _sceneStartedAtMs = Time.GetTicksMsec();

      CreateView();

      var handoff = MultiplayerSessionHandoff.Consume();
      if (handoff != null) {
        AdoptHandoff(handoff);
      }
      else {
        StartDirectJoinFallback();
      }
    }

    private void AdoptHandoff(MultiplayerSessionHandoff handoff) {
      _logger = handoff.Logger;
      _transport = handoff.Transport;
      _flow = handoff.Flow;
      _session = handoff.Session;
      _simulationCallbacks = handoff.SimulationCallbacks;
      _viewCallbacks = handoff.ViewCallbacks;
      _driver = handoff.Driver;
      _simCfg = handoff.SimulationConfig;
      _sesCfg = handoff.SessionConfig;

      _simulationCallbacks.SetInput(Input);
      _viewCallbacks.SetHud(Hud);
      _driver.PreSessionUpdate += CaptureRunningInput;
      OnSessionReady(autoReady: false);
    }

    private void StartDirectJoinFallback() {
      _logger = CreateLogger();
      _registry = LoadAssetRegistry();
      _simCfg = new SimulationConfig { Mode = NetworkMode.ServerDriven };
      _sesCfg = new SessionConfig { MaxPlayers = 2, MinPlayers = 2, CountdownDurationMs = 0 };
      _transport = new LiteNetLibTransport(_logger, connectionKey: ConnectionKey);
      _simulationCallbacks = new SimulationCallbacks(Input);
      _viewCallbacks = new ViewCallbacks(Hud);

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
          onStarted: _driver.TrackConnection);
    }

    private void CreateView() {
      _pool = new DefaultGodotEntityViewPool();
      var playerScene = GD.Load<PackedScene>("res://Shared/Player.tscn");
      var baseScene = GD.Load<PackedScene>("res://Shared/Base.tscn");
      var minionScene = GD.Load<PackedScene>("res://Shared/Minion.tscn");
      _pool.Prewarm(playerScene, 2);
      _pool.Prewarm(baseScene, 2);
      _pool.Prewarm(minionScene, 64);

      _view = new EntityViewUpdaterNode();
      AddChild(_view);
      Input.BindViewRoot(_view);
    }

    private PlayerViewFactory CreateFactory() {
      var playerScene = GD.Load<PackedScene>("res://Shared/Player.tscn");
      var baseScene = GD.Load<PackedScene>("res://Shared/Base.tscn");
      var minionScene = GD.Load<PackedScene>("res://Shared/Minion.tscn");
      return new PlayerViewFactory(playerScene, baseScene, minionScene);
    }

    private void OnSessionReady(bool autoReady) {
      _view.Initialize(_session.Engine, CreateFactory(), _pool);
      _view.PlayerViews.OnLocalViewRegistered += OnLocalViewRegistered;
      _view.PlayerViews.OnLocalViewUnregistered += OnLocalViewUnregistered;
      Hud.SetPhase(_session.Phase);

      if (autoReady)
        SendReady();
    }

    private void SendReady() {
      if (_session == null || _autoReadySent) return;
      Hud.SetLocalReady(true);
      _session.SetReady(true);
      _autoReadySent = true;
      _logger?.KInformation($"[Client] auto-ready sent from multiplayer scene.");
    }

    private void CaptureRunningInput(KlothoSession session, float dt) {
      if (session.State == KlothoState.Running)
        Input.CaptureInput();
    }

    private void OnLocalViewRegistered(EntityViewNode view) {
      _camera?.SetFollowTarget(view);
      var frame = view.Engine?.PredictedFrame.Frame;
      if (frame != null && frame.Has<OwnerComponent>(view.EntityRef))
        Input.SetLocalOwnerId(frame.GetReadOnly<OwnerComponent>(view.EntityRef).OwnerId);
      Input.SelectSingleView(view);
    }

    private void OnLocalViewUnregistered(EntityViewNode view) {
      _camera?.SetFollowTarget(null);
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
          OnSessionReady(autoReady: true);
        }
      }

      UpdateStartDelayHud();

      if (_session == null) return;

      Hud.SetPhase(_session.Phase);
      if (!_autoReadySent && _session.Phase == SessionPhase.Synchronized)
        SendReady();

      AutoTestStep();
    }

    private void UpdateStartDelayHud() {
      double elapsedSeconds = (Time.GetTicksMsec() - _sceneStartedAtMs) / 1000.0;
      double remaining = StartDelaySeconds - elapsedSeconds;
      if (remaining > 0)
        Hud.SetStartDelayRemaining(remaining);
    }

    private void AutoTestStep() {
      if (_session == null) return;
      if (!_verified && _session.State == KlothoState.Running && _session.Engine.CurrentTick >= VerifyTick) {
        _verified = true;
        int n = _view.GetChildCount();
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
      _view?.Cleanup();
      _viewCallbacks?.Cleanup();
      _pool?.Dispose();
      base._ExitTree();
    }
  }
}
