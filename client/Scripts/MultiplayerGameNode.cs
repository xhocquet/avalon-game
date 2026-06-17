// Avalon bootstrap — standalone Server-Driven client.
using System;
using System.Threading.Tasks;
using global::Godot;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Godot;
using xpTURN.Klotho.LiteNetLib;
using xpTURN.Klotho.Logging;
using xpTURN.Klotho.Network;

namespace Meesles.Avalon
{
  public partial class MultiplayerGameNode : GameNode
  {
    private const string ConnectionKey = "Meesles.Avalon";
    private const int RoomId = 0;

    private IKLogger _logger;
    private IDataAssetRegistry _registry;
    private LiteNetLibTransport _transport;
    private KlothoSessionFlow _flow;
    private KlothoSession _session;
    private ViewCallbacks _viewCallbacks;
    private EntityViewUpdaterNode _view;
    private PlayerViewFactory _factory;
    private DefaultGodotEntityViewPool _pool;
    private GodotSessionDriver _driver;
    private ISimulationConfig _simCfg;
    private ISessionConfig _sesCfg;
    private Task<KlothoSession> _joinTask;
    private bool _joining;

    private bool _autoJoin;
    private bool _autoReadySent;
    private bool _verified;
    private const int VerifyTick = 120;

    public override void _Ready()
    {
      WarmupRegistry.RunAll();

      _logger = CreateLogger();
      _registry = LoadAssetRegistry();
      _simCfg = new SimulationConfig();
      _sesCfg = new SessionConfig { MaxPlayers = 2, MinPlayers = 2, CountdownDurationMs = 3000 };
      InitializeSharedNodes();
      _transport = new LiteNetLibTransport(_logger, connectionKey: ConnectionKey);

      Menu.SetMultiplayerMode();
      Hud.SetMultiplayerMode();
      _viewCallbacks = new ViewCallbacks(Hud);

      _flow = new KlothoSessionFlow(
          new KlothoFlowSetupBuilder((s, ss) =>
                  new SessionCallbacks(new SimulationCallbacks(Input), _viewCallbacks))
              .WithLogger(_logger)
              .WithTransport(_transport)
              .WithAssetRegistry(_registry)
              .WithGodotDefaults()
              .Build()
      );

      var playerScene = GD.Load<PackedScene>("res://Shared/Player.tscn");
      _factory = new PlayerViewFactory(playerScene);
      _pool = new DefaultGodotEntityViewPool();
      _pool.Prewarm(playerScene, _sesCfg.MaxPlayers);
      _view = new EntityViewUpdaterNode();
      AddChild(_view);

      _driver = new GodotSessionDriver();
      AddChild(_driver);
      _driver.BindTransport(_transport);
      _driver.PreSessionUpdate += (s, dt) => { if (s.State == KlothoState.Running) Input.CaptureInput(); };

      Menu.OnJoinClicked += OnJoin;
      Menu.OnReadyClicked += OnReady;
      Menu.OnStopClicked += OnStop;
      Menu.SetInitialHost("127.0.0.1", 7777);
      Menu.SetReadyEnabled(false);
      Menu.SetStopEnabled(false);

      SetupView3D();

      foreach (var a in OS.GetCmdlineUserArgs())
      {
        if (a == "join") { _autoJoin = true; OnJoin(); }
      }
    }

    private void OnJoin()
    {
      if (_session != null || _joining) return;
      _joining = true;
      _joinTask = _flow.JoinServerDrivenAsync(_transport, Menu.Host, Menu.Port, RoomId, _sesCfg);
    }

    private void OnReady()
    {
      if (_session == null) return;
      Hud.SetLocalReady(true);
      _session.SetReady(true);
    }

    private void OnStop()
    {
      if (_session == null) return;
      _driver.DetachAndStop();
      _view.Cleanup();
      _viewCallbacks.Cleanup();
      _session = null;
      Menu.SetReadyEnabled(false);
      Menu.SetStopEnabled(false);
      Hud.SetLocalReady(false);
    }

    private void OnSessionReady()
    {
      _view.Initialize(_session.Engine, _factory, _pool);
      _driver.Attach(_session);
      Hud.SetPhase(_session.Phase);
      Menu.SetReadyEnabled(true);
      Menu.SetStopEnabled(true);
    }

    public override void _Process(double delta)
    {
      if (_transport == null) return;

      if (_joining && _joinTask != null)
      {
        if (_joinTask.IsFaulted)
        {
          _logger.KError($"[Client] join failed (server running?): {_joinTask.Exception?.GetBaseException().Message}");
          _joining = false;
          _joinTask = null;
          if (_autoJoin && DisplayServer.GetName() == "headless") GetTree().Quit(1);
        }
        else if (_joinTask.IsCompleted)
        {
          _session = _joinTask.Result;
          _joining = false;
          _joinTask = null;
          OnSessionReady();
        }
      }

      if (_session == null) return;

      Hud.SetPhase(_session.Phase);

      if (_autoJoin) AutoTestStep();
    }

    private void AutoTestStep()
    {
      if (!_autoReadySent && _session.Phase == SessionPhase.Synchronized)
      {
        OnReady();
        _autoReadySent = true;
        _logger.KInformation($"[Client] auto-join ready sent.");
      }

      if (!_verified && _session.State == KlothoState.Running && _session.Engine.CurrentTick >= VerifyTick)
      {
        _verified = true;
        int n = _view.GetChildCount();
        _logger.KInformation($"[Client] auto-join tick={_session.Engine.CurrentTick} viewNodes={n}");
        if (n >= 1) _logger.KInformation($"=== CLIENT OK ===");
        else _logger.KError($"=== CLIENT FAILED (viewNodes={n}) ===");
        if (DisplayServer.GetName() == "headless") GetTree().Quit(n >= 1 ? 0 : 1);
      }
    }

    private static IKLogger CreateLogger()
        => GodotKlothoLogger.CreateDefault(filePrefix: "Client", categoryName: "Client");

    private IDataAssetRegistry LoadAssetRegistry()
    {
      byte[] bytes = global::Godot.FileAccess.GetFileAsBytes("res://Data/Assets.bytes");
      if (bytes == null || bytes.Length == 0)
      {
        var err = global::Godot.FileAccess.GetOpenError();
        throw new System.IO.FileNotFoundException($"res://Data/Assets.bytes not found (err={err})");
      }
      var assets = DataAssetReader.LoadMixedCollectionFromBytes(bytes);
      IDataAssetRegistryBuilder builder = new DataAssetRegistry();
      builder.RegisterRange(assets);
      return builder.Build();
    }

    public override void _ExitTree()
    {
      if (_session != null) { _driver?.DetachAndStop(); _session = null; }
      _view?.Cleanup();
      _viewCallbacks?.Cleanup();
      _pool?.Dispose();
      base._ExitTree();
    }
  }
}
