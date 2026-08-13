using System.Threading.Tasks;
using Godot;
using Meesles.Avalon.Client;
using Meesles.Avalon.Client.Scripts;
using Meesles.Avalon.Client.Scripts.View;
using Meesles.Avalon.Sim.Network;
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
    // The lobby only ever drives the server-driven session, and the server picks its own map, so
    // this session's data is the networked game type's regardless of what the cards are showing.
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
    // Kept as a field because the display name is not known at build time — the player types it after
    // this runs. The flow reads ClaimedDisplayName off this instance at connect, so OnJoin stamps it.
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
    LobbyUi.OnReadyClicked += OnReady;
    LobbyUi.OnUnreadyClicked += OnUnready;
    LobbyUi.OnStopClicked += OnStop;
    LobbyUi.OnFactionSelected += OnFactionSelected;
    LobbyUi.OnStartLocalClicked += OnStartLocal;
    LobbyUi.SetInitialHost(ServerEndpoint.Host, ServerEndpoint.Port);
    LobbyUi.SetReadyEnabled(false);
    LobbyUi.SetStopEnabled(false);

    _quickplay = QuickplayLaunch.Consume();
    ApplyFactionArg();
    ApplyNameArg();
    ApplyGameTypeArg();
    if (_quickplay)
      CallDeferred(GameTypeCatalog.Selected.IsLocal ? MethodName.OnStartLocal : MethodName.OnJoin);
  }

  // `--gametype=<id>` mirrors --faction: it picks a GameTypeCatalog entry without the lobby UI, so a
  // playground can be launched straight from a script.
  private void ApplyGameTypeArg() {
    foreach (var arg in OS.GetCmdlineUserArgs()) {
      if (!arg.StartsWith("--gametype=")) continue;
      var value = arg["--gametype=".Length..].Trim();
      if (GameTypeCatalog.Exists(value))
        LobbyUi.SetGameType(value);
      else
        _logger.KError($"[Client] --gametype value '{value}' is not a known game type id.");
      return;
    }
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

  // `--name=<display name>`, the same escape hatch as --faction: headless clients have no one to type
  // into the name field, so distinct rosters are only testable from the command line.
  private void ApplyNameArg() {
    foreach (var arg in OS.GetCmdlineUserArgs()) {
      if (!arg.StartsWith("--name=")) continue;
      var value = arg["--name=".Length..].Trim();
      if (value.Length > 0) {
        PlayerProfile.PlayerName = value;
        LobbyUi.SetPlayerName(value);
      }

      return;
    }
  }

  private void OnJoin() {
    if (_session != null || _joining) return;
    _joining = true;
    LobbyUi.SetGameTypeEnabled(false);

    // Rides along in the join handshake as PlayerJoinMessage.ClaimedDisplayName. With no lobby server
    // issuing identity tickets, the server takes this at face value and publishes it as the roster's
    // DisplayName, which is what every other client renders. Unverified by design — spoofable until a
    // real identity provider is wired (see Klotho's LobbyIntegrationGuide).
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
    LobbyUi.SetReadyState(true);
  }

  private void OnUnready() {
    if (_session == null) return;
    LobbyUi.SetLocalReady(false);
    _session.SetReady(false);
    LobbyUi.SetReadyState(false);
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
    LobbyUi.SetReadyState(false);
    LobbyUi.SetStopEnabled(false);
    LobbyUi.SetLocalReady(false);
    LobbyUi.SetPhase(SessionPhase.Disconnected);
    LobbyUi.SetConnected(false);
    LobbyUi.SetGameTypeEnabled(true);
  }

  // Local game types skip the whole join/ready handshake — the game scene hosts its own session.
  private void OnStartLocal() {
    var gameType = GameTypeCatalog.Selected;
    if (!gameType.IsLocal) return;

    _logger.KInformation($"[Client] starting local game type '{gameType.Id}' -> {gameType.GameScenePath}");
    GetTree().ChangeSceneToFile(gameType.GameScenePath);
  }

  private void OnSessionReady() {
    _driver.Attach(_session);
    _session.Engine.OnPlayerConfigReceived += OnPlayerConfigReceived;
    // The server only broadcasts a config at the moment it arrives, so a player who joins later
    // never hears about picks made before them. Every peer re-announces on a join and the roster
    // converges — cheap, since this is one reliable 16-byte message per lobby event.
    _session.NetworkService.OnPlayerJoined += OnPlayerJoined;
    _configDirty = true;

    LobbyUi.SetPhase(_session.Phase);
    LobbyUi.SetConnected(true);
    LobbyUi.SetReadyEnabled(true);
    LobbyUi.SetStopEnabled(true);
  }

  // ---------------------------------------------------------------- faction pick propagation

  private void OnFactionSelected(int factionId) {
    _configDirty = true;
  }

  private void OnPlayerJoined(IPlayerInfo player) {
    _configDirty = true;
  }

  // Roster names arrive by two different routes — the handshake reply for players already in the room,
  // and a join notification for later arrivals — so log on size change rather than per event.
  private void LogRosterChanges() {
    var players = _session.NetworkService.Players;
    if (players.Count == _loggedRosterCount) return;
    _loggedRosterCount = players.Count;
    foreach (var p in players)
      _logger.KInformation($"[Client] lobby roster: p{p.PlayerId} '{p.DisplayName}'");
  }

  // Sends the local pick over Klotho's PlayerConfig channel (client -> server -> all peers). This is
  // lobby presentation only; the sim still gets the faction from SelectFactionCommand at match start.
  private void PushFactionConfig() {
    if (_session == null) return;
    if (!_configDirty && FactionSelection.SelectedFactionId == _lastSentFactionId) return;

    // LocalPlayerId lands with the handshake; before that the server would file the config under a
    // bogus id, so hold off and retry next frame.
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

    GetTree().ChangeSceneToFile(GameScenePath);
  }

  // The session (and its engine) outlives this scene on handoff, so the lobby's subscriptions have to
  // come off explicitly — otherwise they keep firing into a freed LobbyUI.
  public override void _ExitTree() {
    UnsubscribeSession();
    base._ExitTree();
  }
}
