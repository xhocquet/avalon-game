using Godot;
using Meesles.Avalon.Client;
using Meesles.Avalon.Sim.Models;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Godot;
using xpTURN.Klotho.LiteNetLib;
using xpTURN.Klotho.Network;

namespace Meesles.Avalon;

public partial class SingleplayerGameNode : GameNode {
  private const string ConnectionKey = "Meesles.Avalon.Singleplayer";
  private CameraController _camera;
  private GodotSessionDriver _driver;
  private SimEventHub _events;
  private FactionCatalog _factions;
  private KlothoSessionFlow _flow;
  private DefaultGodotEntityViewPool _pool;
  private ISessionConfig _sesCfg;
  private KlothoSession _session;
  private ISimulationConfig _simCfg;

  private LiteNetLibTransport _transport;
  private VfxManager _vfx;
  private EntityViewUpdaterNode _view;
  private ViewCallbacks _viewCallbacks;

  public override void _Ready() {
    WarmupRegistry.RunAll();

    var logger = CreateLogger("Singleplayer");
    var registry = LoadAssetRegistry();
    var navMeshBytes = LoadNavigationMeshBytes();
    _simCfg = new SimulationConfig {
      InputDelayTicks = 1,
      InterpolationDelayTicks = 1
    };
    _sesCfg = new SessionConfig { MaxPlayers = 1, MinPlayers = 1, CountdownDurationMs = 0 };

    InitializeGameUI();
    GameUi.SetMultiplayerMode();
    GameUi.SetPhase(SessionPhase.Playing);

    _camera = GetNodeOrNull<CameraController>("Camera3D");
    Input.BindCamera(_camera);

    _viewCallbacks = new ViewCallbacks(GameUi);
    _transport = new LiteNetLibTransport(logger, connectionKey: ConnectionKey);
    _flow = new KlothoSessionFlow(
      new KlothoFlowSetupBuilder((s, ss) =>
          new SessionCallbacks(new SimCallbacks(Input, navMeshBytes, logger), _viewCallbacks))
        .WithLogger(logger)
        .WithTransport(_transport)
        .WithAssetRegistry(registry)
        .WithGodotDefaults()
        .Build()
    );

    _driver = new GodotSessionDriver();
    AddChild(_driver);
    _driver.BindTransport(_transport);
    _driver.PreSessionUpdate += (s, dt) => {
      if (s.State == KlothoState.Running) Input.CaptureInput();
    };

    CreateView();
    StartLocalSession();
  }

  private void ResetSession() {
    StopSession();
    StartLocalSession();
  }

  private void StartLocalSession() {
    _session = _flow.StartHost(_simCfg, _sesCfg);
    _session.HostGame("Local", _sesCfg.MaxPlayers);

    _view.Initialize(_session.Engine, CreateFactory(), _pool);
    _view.PlayerViews.OnLocalViewRegistered += OnLocalViewRegistered;
    _view.PlayerViews.OnLocalViewUnregistered += OnLocalViewUnregistered;
    _events = new SimEventHub();
    _events.Attach(_session.Engine);
    _vfx = new VfxManager();
    _vfx.Attach(_events, _view);
    GameUi.BindSimEvents(_events);

    _driver.Attach(_session);
    GameUi.SetLocalPlayerId(_session.LocalPlayerId);
    GameUi.HideResult();
    _session.SetReady(true);
  }

  private void StopSession() {
    UnbindCameraFollow();
    _vfx?.Detach();
    _events?.Detach();
    _driver?.DetachAndStop(saveReplay: false);
    _view?.Cleanup();
    _session = null;
  }

  private void CreateView() {
    _pool = new DefaultGodotEntityViewPool();
    _factions = FactionCatalog.CreateDefault();
    var crystalScene = GD.Load<PackedScene>("res://Scenes/Objects/Crystal.tscn");
    var turretScene = GD.Load<PackedScene>("res://Scenes/Objects/Turret.tscn");
    foreach (var faction in _factions.Entries) {
      _pool.Prewarm(faction.HeroScene, _sesCfg.MaxPlayers);
      _pool.Prewarm(faction.MinionScene, 64);
    }

    _pool.Prewarm(crystalScene, _sesCfg.MaxPlayers);
    _pool.Prewarm(turretScene, _sesCfg.MaxPlayers * 2);

    _view = new EntityViewUpdaterNode();
    AddChild(_view);
    Input.BindViewRoot(_view);
  }

  private UnitViewFactory CreateFactory() {
    var crystalScene = GD.Load<PackedScene>("res://Scenes/Objects/Crystal.tscn");
    var turretScene = GD.Load<PackedScene>("res://Scenes/Objects/Turret.tscn");
    return new UnitViewFactory(_factions, crystalScene, turretScene);
  }

  private void OnLocalViewRegistered(EntityViewNode view) {
    _camera?.SetFollowTarget(view);
    var frame = view.Engine?.PredictedFrame.Frame;
    if (frame != null && frame.Has<Team>(view.EntityRef))
      Input.SetLocalTeamId(frame.GetReadOnly<Team>(view.EntityRef).TeamId);
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

  public override void _ExitTree() {
    StopSession();
    _pool?.Dispose();
    _viewCallbacks?.Cleanup();
    base._ExitTree();
  }
}
