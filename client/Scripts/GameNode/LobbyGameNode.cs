using System;
using System.Threading.Tasks;
using Godot;
using Meesles.Avalon.Client;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Godot;
using xpTURN.Klotho.LiteNetLib;
using xpTURN.Klotho.Logging;
using xpTURN.Klotho.Network;

namespace Meesles.Avalon;

public partial class LobbyGameNode : GameNode {
  private const string ConnectionKey = "Meesles.Avalon";
  private const int RoomId = 0;
  private const string GameScenePath = "res://Scenes/Multiplayer.tscn";
  private const int CountdownMs = 1000;
  private bool _autoReadySent;
  private ulong _countdownStartedAtMs;
  private GodotSessionDriver _driver;
  private KlothoSessionFlow _flow;
  private bool _handoffStarted;
  private bool _joining;
  private Task<KlothoSession> _joinTask;
  private SessionPhase _lastPhase = SessionPhase.None;

  private IKLogger _logger;
  private bool _quickplay;
  private IDataAssetRegistry _registry;
  private ISessionConfig _sesCfg;
  private KlothoSession _session;
  private ISimulationConfig _simCfg;
  private SimCallbacks _simulationCallbacks;
  private LiteNetLibTransport _transport;
  private ViewCallbacks _viewCallbacks;

  public override void _Ready() {
    WarmupRegistry.RunAll();

    _logger = CreateLogger();
    _registry = LoadAssetRegistry();
    var navMeshBytes = LoadNavigationMeshBytes();
    _simCfg = new SimulationConfig {
      Mode = NetworkMode.ServerDriven,
      InputDelayTicks = 2,
      SDInputLeadTicks = 2,
      InterpolationDelayTicks = 2,
      UsePrediction = true,
      EnableErrorCorrection = true
    };
    _sesCfg = new SessionConfig { MaxPlayers = 2, MinPlayers = 2, CountdownDurationMs = CountdownMs };

    InitializeSharedNodes();
    Menu.SetLobbyMode();
    LobbyUi.SetLobbyMode();

    _simulationCallbacks = new SimCallbacks(Input, navMeshBytes, _logger);
    _viewCallbacks = new ViewCallbacks(LobbyUi);
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
    Menu.OnUnreadyClicked += OnUnready;
    Menu.OnStopClicked += OnStop;
    Menu.SetInitialHost("127.0.0.1", 7777);
    Menu.SetReadyEnabled(false);
    Menu.SetStopEnabled(false);

    _quickplay = Array.IndexOf(OS.GetCmdlineUserArgs(), "--quickplay") >= 0;
    ApplyFactionArg();
    if (_quickplay) CallDeferred(MethodName.OnJoin);
  }

  // Lets `--faction=<id>` on the command line pick a faction without touching the lobby UI,
  // so quickplay.ps1 can launch differently-factioned clients for testing.
  private void ApplyFactionArg() {
    foreach (var arg in OS.GetCmdlineUserArgs()) {
      if (!arg.StartsWith("--faction=")) continue;
      var value = arg["--faction=".Length..];
      if (int.TryParse(value, out var factionId))
        FactionSelection.SelectedFactionId = factionId;
      else
        _logger.KError($"[Client] --faction value '{value}' is not a valid faction id.");
      return;
    }
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
      _driver.TrackConnection);
  }

  private void OnReady() {
    if (_session == null) return;
    LobbyUi.SetLocalReady(true);
    _session.SetReady(true);
    Menu.SetReadyState(true);
  }

  private void OnUnready() {
    if (_session == null) return;
    LobbyUi.SetLocalReady(false);
    _session.SetReady(false);
    Menu.SetReadyState(false);
  }

  private void OnStop() {
    if (_session != null) {
      _driver.DetachAndStop();
      _session = null;
    }

    Menu.SetReadyEnabled(false);
    Menu.SetReadyState(false);
    Menu.SetStopEnabled(false);
    LobbyUi.SetLocalReady(false);
    LobbyUi.SetPhase(SessionPhase.Disconnected);
    LobbyUi.SetConnected(false);
  }

  private void OnSessionReady() {
    _driver.Attach(_session);
    LobbyUi.SetPhase(_session.Phase);
    LobbyUi.SetConnected(true);
    Menu.SetReadyEnabled(true);
    Menu.SetStopEnabled(true);
  }

  public override void _Process(double delta) {
    if (_joining && _joinTask != null) {
      if (_joinTask.IsFaulted) {
        _logger.KError($"[Client] join failed (server running?): {_joinTask.Exception?.GetBaseException().Message}");
        _joining = false;
        _joinTask = null;
        LobbyUi.SetConnected(false);
      }
      else if (_joinTask.IsCompleted) {
        _session = _joinTask.Result;
        _joining = false;
        _joinTask = null;
        OnSessionReady();
      }
    }

    if (_session == null) return;

    LobbyUi.SetPhase(_session.Phase);
    UpdateCountdownHud(_session.Phase);
    AutoReadyHeadless();
    LobbyUi.SyncPlayers(_session.NetworkService.Players, _session.NetworkService.LocalPlayerId);

    if (_session.Phase == SessionPhase.Playing)
      StartGameScene();
  }

  private void AutoReadyHeadless() {
    if (_autoReadySent) return;
    if (DisplayServer.GetName() != "headless" && !_quickplay) return;
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
        LobbyUi.SetCountdownRemaining(_sesCfg.CountdownDurationMs / 1000.0);
      }
    }

    if (phase != SessionPhase.Countdown) return;

    var elapsedSeconds = (Time.GetTicksMsec() - _countdownStartedAtMs) / 1000.0;
    LobbyUi.SetCountdownRemaining(_sesCfg.CountdownDurationMs / 1000.0 - elapsedSeconds);
  }

  private void StartGameScene() {
    if (_handoffStarted) return;
    _handoffStarted = true;

    MultiplayerSessionHandoff.Store(new MultiplayerSessionHandoff {
      Logger = _logger,
      LoggerFactory = LoggerFactory,
      Transport = _transport,
      Flow = _flow,
      Session = _session,
      SimulationCallbacks = _simulationCallbacks,
      ViewCallbacks = _viewCallbacks,
      Driver = _driver,
      SimulationConfig = _simCfg,
      SessionConfig = _sesCfg
    });
    LoggerFactory = null;

    GetTree().ChangeSceneToFile(GameScenePath);
  }
}
