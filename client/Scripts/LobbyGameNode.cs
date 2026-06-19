using System.Threading.Tasks;
using global::Godot;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Godot;
using xpTURN.Klotho.LiteNetLib;
using xpTURN.Klotho.Logging;
using xpTURN.Klotho.Network;

namespace Meesles.Avalon {
  public partial class LobbyGameNode : GameNode {
    private const string ConnectionKey = "Meesles.Avalon";
    private const int RoomId = 0;
    private const string GameScenePath = "res://Scenes/Multiplayer.tscn";

    private IKLogger _logger;
    private IDataAssetRegistry _registry;
    private LiteNetLibTransport _transport;
    private KlothoSessionFlow _flow;
    private KlothoSession _session;
    private SimulationCallbacks _simulationCallbacks;
    private ViewCallbacks _viewCallbacks;
    private GodotSessionDriver _driver;
    private ISimulationConfig _simCfg;
    private ISessionConfig _sesCfg;
    private Task<KlothoSession> _joinTask;
    private bool _joining;
    private bool _handoffStarted;
    private bool _autoReadySent;
    private SessionPhase _lastPhase = SessionPhase.None;
    private ulong _countdownStartedAtMs;

    public override void _Ready() {
      WarmupRegistry.RunAll();

      _logger = CreateLogger();
      _registry = LoadAssetRegistry();
      _simCfg = new SimulationConfig { Mode = NetworkMode.ServerDriven };
      _sesCfg = new SessionConfig { MaxPlayers = 2, MinPlayers = 2, CountdownDurationMs = 3000 };

      InitializeSharedNodes();
      Menu.SetLobbyMode();
      Hud.SetLobbyMode();

      _simulationCallbacks = new SimulationCallbacks(Input);
      _viewCallbacks = new ViewCallbacks(Hud);
      _transport = new LiteNetLibTransport(_logger, connectionKey: ConnectionKey);
      _flow = new KlothoSessionFlow(
          new KlothoFlowSetupBuilder((s, ss) =>
                  new SessionCallbacks(_simulationCallbacks, _viewCallbacks))
              .WithLogger(_logger)
              .WithTransport(_transport)
              .WithAssetRegistry(_registry)
              .WithGodotDefaults()
              .Build()
      );

      _driver = new GodotSessionDriver { Name = "KlothoSessionDriver" };
      GetTree().Root.CallDeferred(Node.MethodName.AddChild, _driver);
      _driver.BindTransport(_transport);

      Menu.OnJoinClicked += OnJoin;
      Menu.OnReadyClicked += OnReady;
      Menu.OnStopClicked += OnStop;
      Menu.SetInitialHost("127.0.0.1", 7777);
      Menu.SetReadyEnabled(false);
      Menu.SetStopEnabled(false);

#if DEBUG
      OnJoin();
#endif
    }

    private void OnJoin() {
      if (_session != null || _joining) return;
      _joining = true;
      _joinTask = _flow.JoinServerDrivenAsync(
          _transport,
          Menu.Host,
          Menu.Port,
          RoomId,
          _sesCfg,
          onStarted: _driver.TrackConnection);
    }

    private void OnReady() {
      if (_session == null) return;
      Hud.SetLocalReady(true);
      _session.SetReady(true);
      Menu.SetReadyEnabled(false);
    }

    private void OnStop() {
      if (_session != null) {
        _driver.DetachAndStop();
        _session = null;
      }

      Menu.SetReadyEnabled(false);
      Menu.SetStopEnabled(false);
      Hud.SetLocalReady(false);
      Hud.SetPhase(SessionPhase.Disconnected);
    }

    private void OnSessionReady() {
      _driver.Attach(_session);
      Hud.SetPhase(_session.Phase);
      Menu.SetReadyEnabled(true);
      Menu.SetStopEnabled(true);
    }

    public override void _Process(double delta) {
      if (_joining && _joinTask != null) {
        if (_joinTask.IsFaulted) {
          _logger.KError($"[Client] join failed (server running?): {_joinTask.Exception?.GetBaseException().Message}");
          _joining = false;
          _joinTask = null;
        }
        else if (_joinTask.IsCompleted) {
          _session = _joinTask.Result;
          _joining = false;
          _joinTask = null;
          OnSessionReady();
        }
      }

      if (_session == null) return;

      Hud.SetPhase(_session.Phase);
      UpdateCountdownHud(_session.Phase);
      AutoReadyHeadless();

      if (_session.Phase == SessionPhase.Playing)
        StartGameScene();
    }

    private void AutoReadyHeadless() {
      if (_autoReadySent) return;
      if (DisplayServer.GetName() != "headless") return;
      if (_session.Phase != SessionPhase.Synchronized) return;

      OnReady();
      _autoReadySent = true;
      _logger.KInformation($"[Client] lobby auto-ready sent.");
    }

    private void UpdateCountdownHud(SessionPhase phase) {
      if (phase != _lastPhase) {
        _lastPhase = phase;
        if (phase == SessionPhase.Countdown) {
          _countdownStartedAtMs = Time.GetTicksMsec();
          Hud.SetCountdownRemaining(_sesCfg.CountdownDurationMs / 1000.0);
        }
      }

      if (phase != SessionPhase.Countdown) return;

      double elapsedSeconds = (Time.GetTicksMsec() - _countdownStartedAtMs) / 1000.0;
      Hud.SetCountdownRemaining((_sesCfg.CountdownDurationMs / 1000.0) - elapsedSeconds);
    }

    private void StartGameScene() {
      if (_handoffStarted) return;
      _handoffStarted = true;

      MultiplayerSessionHandoff.Store(new MultiplayerSessionHandoff {
        Logger = _logger,
        Transport = _transport,
        Flow = _flow,
        Session = _session,
        SimulationCallbacks = _simulationCallbacks,
        ViewCallbacks = _viewCallbacks,
        Driver = _driver,
        SimulationConfig = _simCfg,
        SessionConfig = _sesCfg,
      });

      GetTree().ChangeSceneToFile(GameScenePath);
    }
  }
}
