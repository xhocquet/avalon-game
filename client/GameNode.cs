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
  public partial class GameNode : Node
  {
    private const string ConnectionKey = "Meesles.Avalon";
    private const int RoomId = 0;

    private IKLogger _logger;
    private IDataAssetRegistry _registry;
    private LiteNetLibTransport _transport;
    private InputCapture _input;
    private KlothoSessionFlow _flow;
    private KlothoSession _session;
    private ViewCallbacks _viewCallbacks;
    private EntityViewUpdaterNode _view;
    private PlayerViewFactory _factory;
    private DefaultGodotEntityViewPool _pool;
    private GodotSessionDriver _driver;
    private Menu _menu;
    private Hud _hud;

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
      _input = new InputCapture();
      _transport = new LiteNetLibTransport(_logger, connectionKey: ConnectionKey);

      _menu = GetNode<Menu>("UILayer/Menu");
      _hud = GetNode<Hud>("UILayer/Hud");
      _viewCallbacks = new ViewCallbacks(_hud);

      _flow = new KlothoSessionFlow(
          new KlothoFlowSetupBuilder((s, ss) =>
                  new SessionCallbacks(new SimulationCallbacks(_input), _viewCallbacks))
              .WithLogger(_logger)
              .WithTransport(_transport)
              .WithAssetRegistry(_registry)
              .WithGodotDefaults()
              .Build()
      );

      var playerScene = GD.Load<PackedScene>("res://player.tscn");
      _factory = new PlayerViewFactory(playerScene);
      _pool = new DefaultGodotEntityViewPool();
      _pool.Prewarm(playerScene, _sesCfg.MaxPlayers);
      _view = new EntityViewUpdaterNode();
      AddChild(_view);

      _driver = new GodotSessionDriver();
      AddChild(_driver);
      _driver.BindTransport(_transport);
      _driver.PreSessionUpdate += (s, dt) => { if (s.State == KlothoState.Running) _input.CaptureInput(); };

      _menu.OnJoinClicked += OnJoin;
      _menu.OnReadyClicked += OnReady;
      _menu.OnStopClicked += OnStop;
      _menu.SetInitialHost("127.0.0.1", 7777);
      _menu.SetReadyEnabled(false);
      _menu.SetStopEnabled(false);

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
      _joinTask = _flow.JoinServerDrivenAsync(_transport, _menu.Host, _menu.Port, RoomId, _sesCfg);
    }

    private void OnReady()
    {
      if (_session == null) return;
      _hud.SetLocalReady(true);
      _session.SetReady(true);
    }

    private void OnStop()
    {
      if (_session == null) return;
      _driver.DetachAndStop();
      _view.Cleanup();
      _viewCallbacks.Cleanup();
      _session = null;
      _menu.SetReadyEnabled(false);
      _menu.SetStopEnabled(false);
      _hud.SetLocalReady(false);
    }

    private void OnSessionReady()
    {
      _view.Initialize(_session.Engine, _factory, _pool);
      _driver.Attach(_session);
      _hud.SetPhase(_session.Phase);
      _menu.SetReadyEnabled(true);
      _menu.SetStopEnabled(true);
    }

    public override void _Process(double delta)
    {
      if (_transport == null) return;

      if (_joining && _joinTask != null)
      {
        if (_joinTask.IsFaulted)
        {
          _logger.KError($"[Client] join failed (server running?): {_joinTask.Exception?.GetBaseException().Message}");
          _joining = false; _joinTask = null;
          if (_autoJoin && DisplayServer.GetName() == "headless") GetTree().Quit(1);
        }
        else if (_joinTask.IsCompleted)
        {
          _session = _joinTask.Result;
          _joining = false; _joinTask = null;
          OnSessionReady();
        }
      }

      if (_session == null) return;

      _hud.SetPhase(_session.Phase);

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

    private void SetupView3D()
    {
      var cam = GetNodeOrNull<Camera3D>("Camera3D");
      if (cam != null)
      {
        cam.LookAtFromPosition(new Vector3(0, 7, 0), Vector3.Zero, new Vector3(0, 0, -1));
        cam.Environment = new global::Godot.Environment
        {
          BackgroundMode = global::Godot.Environment.BGMode.Color,
          BackgroundColor = new Color(0.12f, 0.13f, 0.18f),
          AmbientLightSource = global::Godot.Environment.AmbientSource.Color,
          AmbientLightColor = new Color(0.5f, 0.5f, 0.5f),
          AmbientLightEnergy = 1.0f,
        };
      }

      var light = GetNodeOrNull<DirectionalLight3D>("DirectionalLight3D");
      light?.LookAtFromPosition(new Vector3(4, 10, 4), Vector3.Zero, Vector3.Up);
    }

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
      _input?.Dispose();
    }
  }
}
