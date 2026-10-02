using System.Threading.Tasks;
using Godot;
using Meesles.Avalon.Client;
using Meesles.Avalon.Client.Scripts;
using Meesles.Avalon.Client.Scripts.GameState;
using Meesles.Avalon.Client.Scripts.View;
using Meesles.Avalon.Sim.Events;
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
  private const int CountdownMs = 1000;
  private bool _autoReadySent;
  private bool _configDirty;
  private ulong _countdownStartedAtMs;
  private GodotSessionDriver _driver;
  private KlothoSessionFlow _flow;
  private KlothoFlowSetup _flowSetup;
  private bool _handoffStarted;
  private bool _joining;
  private Task<KlothoSession> _joinTask;
  private SessionPhase _lastPhase = SessionPhase.None;
  private int _lastSentFactionId = -1;
  private int _loggedRosterCount = -1;

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
    // The lobby always initializes the server map.
    var networked = GameTypeCatalog.Resolve(GameTypeCatalog.DefaultId);
    _registry = LoadAssetRegistry(networked.MapLayoutPath);
    var navMeshBytes = LoadNavigationMeshBytes(networked.NavMeshPath);
    _simCfg = new SimulationConfig {
      Mode = NetworkMode.ServerDriven,
      InputDelayTicks = 2,
      SDInputLeadTicks = 2,
      InterpolationDelayTicks = 2,
      UsePrediction = true,
      EnableErrorCorrection = true
    };
    _sesCfg = new SessionConfig { MaxPlayers = 4, MinPlayers = 2, CountdownDurationMs = CountdownMs };

    InitializeSharedNodes();
    LobbyUi.SetLobbyMode();

    _simulationCallbacks = new SimCallbacks(Input, navMeshBytes, _logger);
    _viewCallbacks = new ViewCallbacks(LobbyUi);
    _transport = new LiteNetLibTransport(_logger, connectionKey: ConnectionKey);
    // ClaimedDisplayName is set when the player joins.
    _flowSetup = new KlothoFlowSetupBuilder((s, ss) =>
        new SessionCallbacks(_simulationCallbacks, _viewCallbacks))
      .WithLogger(_logger)
      .WithTransport(_transport)
      .WithAssetRegistry(_registry)
      .WithGodotDefaults()
      .Build();
    _flow = new KlothoSessionFlow(_flowSetup);

    _driver = new GodotSessionDriver { Name = "KlothoSessionDriver" };
    GetTree().Root.CallDeferred(Node.MethodName.AddChild, _driver);
    _driver.BindTransport(_transport);

    LobbyUi.OnJoinClicked += OnJoin;
    LobbyUi.OnDisconnectClicked += OnStop;
    LobbyUi.OnReadyClicked += OnReady;
    LobbyUi.OnUnreadyClicked += OnUnready;
    LobbyUi.OnFactionSelected += OnFactionSelected;
    LobbyUi.OnStartLocalClicked += OnStartLocal;
    LobbyUi.SetInitialHost(ServerConfig.Host, ServerConfig.Port);
    LobbyUi.SetReadyEnabled(false);

    _quickplay = QuickplayLaunch.Consume();
    ApplyFactionArg();
    ApplyNameArg();
    ApplyGameTypeArg();
    if (_quickplay)
      CallDeferred(GameTypeCatalog.Selected.IsLocal ? MethodName.OnStartLocal : MethodName.OnJoin);
  }

  private void ApplyGameTypeArg() {
    foreach (var arg in OS.GetCmdlineUserArgs()) {
      if (!arg.StartsWith("--gametype=")) continue;
      var value = arg["--gametype=".Length..].Trim();
      if (GameTypeCatalog.Exists(value))
        LobbyUi.SetGameType(value);
      else
        _logger.KError($"[Client] --gametype value '{value}' is not a known game type id.");
    }
  }

  private void ApplyFactionArg() {
    foreach (var arg in OS.GetCmdlineUserArgs()) {
      if (!arg.StartsWith("--faction=")) continue;
      var value = arg["--faction=".Length..];
      if (int.TryParse(value, out var factionId))
        FactionSelection.SelectedFactionId = factionId;
      else
        _logger.KError($"[Client] --faction value '{value}' is not a valid faction id.");
    }
  }

  private void ApplyNameArg() {
    foreach (var arg in OS.GetCmdlineUserArgs()) {
      if (!arg.StartsWith("--name=")) continue;
      var value = arg["--name=".Length..].Trim();
      if (value.Length > 0) {
        PlayerProfile.PlayerName = value;
        LobbyUi.SetPlayerName(value);
      }
    }
  }

  private void OnJoin() {
    if (_session != null || _joining) return;
    _joining = true;
    LobbyUi.SetGameTypeEnabled(false);

    _flowSetup.ClaimedDisplayName = PlayerProfile.PlayerName;

    _joinTask = _flow.JoinServerDrivenAsync(
      _transport,
      LobbyUi.Host,
      LobbyUi.Port,
      RoomId,
      _sesCfg,
      _driver.TrackConnection);
  }

  private void OnReady() {
    if (_session == null) return;
    LobbyUi.SetLocalReady(true);
    _session.SetReady(true);
  }

  private void OnUnready() {
    if (_session == null) return;
    LobbyUi.SetLocalReady(false);
    _session.SetReady(false);
  }

  private void OnStop() {
    if (_session != null) {
      UnsubscribeSession();
      _driver.DetachAndStop();
      _session = null;
      _lastSentFactionId = -1;
      _loggedRosterCount = -1;
    }

    LobbyUi.SetReadyEnabled(false);
    LobbyUi.SetLocalReady(false);
    LobbyUi.SetPhase(SessionPhase.Disconnected);
    LobbyUi.SetConnected(false);
    LobbyUi.SetGameTypeEnabled(true);
  }

  private void OnStartLocal() {
    var gameType = GameTypeCatalog.Selected;
    if (!gameType.IsLocal) return;

    _logger.KInformation($"[Client] starting local game type '{gameType.Id}' -> {gameType.GameScenePath}");
    GetTree().ChangeSceneToFile(gameType.GameScenePath);
  }

  private void OnSessionReady() {
    _driver.Attach(_session);
    _session.Engine.OnPlayerConfigReceived += OnPlayerConfigReceived;
    // Reannounce the local selection when a player joins.
    _session.NetworkService.OnPlayerJoined += OnPlayerJoined;
    _configDirty = true;

    LobbyUi.SetPhase(_session.Phase);
    LobbyUi.SetConnected(true);
    LobbyUi.SetReadyEnabled(true);
  }

  private void OnFactionSelected(int _) {
    _configDirty = true;
  }

  private void OnPlayerJoined(IPlayerInfo _) {
    _configDirty = true;
  }

  // Avoid logging the full roster every frame.
  private void LogRosterChanges() {
    var players = _session.NetworkService.Players;
    if (players.Count == _loggedRosterCount) return;
    _loggedRosterCount = players.Count;
    foreach (var p in players)
      _logger.KInformation($"[Client] lobby roster: p{p.PlayerId} '{p.DisplayName}'");
  }

  private void PushFactionConfig() {
    if (_session == null) return;
    if (!_configDirty && FactionSelection.SelectedFactionId == _lastSentFactionId) return;

    // LocalPlayerId arrives with the handshake.
    if (_session.NetworkService.LocalPlayerId <= 0) return;

    _lastSentFactionId = FactionSelection.SelectedFactionId;
    _configDirty = false;
    _session.SendPlayerConfig(new LobbyPlayerConfig { FactionId = _lastSentFactionId });
  }

  private void OnPlayerConfigReceived(int playerId, bool firstTime) {
    if (!_session.Engine.TryGetPlayerConfig<LobbyPlayerConfig>(playerId, out var config)) return;
    LobbyUi.SetPlayerFaction(playerId, config.FactionId);
    _logger.KInformation($"[Client] lobby faction from p{playerId}: faction={config.FactionId} first={firstTime}");
  }

  private void UnsubscribeSession() {
    if (_session == null) return;
    _session.Engine.OnPlayerConfigReceived -= OnPlayerConfigReceived;
    _session.NetworkService.OnPlayerJoined -= OnPlayerJoined;
  }

  public override void _Process(double delta) {
    if (_joining && _joinTask != null) {
      if (_joinTask.IsFaulted) {
        _logger.KError($"[Client] join failed (server running?): {_joinTask.Exception?.GetBaseException().Message}");
        _joining = false;
        _joinTask = null;
        LobbyUi.SetConnected(false);
        LobbyUi.SetGameTypeEnabled(true);
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
    PushFactionConfig();
    LogRosterChanges();
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

    GetTree().ChangeSceneToFile(Scenes.Multiplayer);
  }

  public override void _ExitTree() {
    UnsubscribeSession();
    base._ExitTree();
  }
}
