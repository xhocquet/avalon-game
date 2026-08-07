using Godot;
using Meesles.Avalon.Client;
using Meesles.Avalon.Client.Scripts.View;
using Meesles.Avalon.Sim;
using Meesles.Avalon.Sim.Components;
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
    BindNavigationToInput();

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

    IKlothoEngine engine = _session.Engine;   // IsResimulation is a default interface member
    SimLog.BindStage(() => engine.IsResimulation);
    Input.BindEngine(engine);
    _view.Initialize(_session.Engine, CreateFactory(), _pool);
    _view.PlayerViews.OnLocalViewRegistered += OnLocalViewRegistered;
    _view.PlayerViews.OnLocalViewUnregistered += OnLocalViewUnregistered;
    _events = new SimEventHub();
    _events.Attach(_session.Engine);
    BindTeamBaseCleanup(_events);
    _vfx = new VfxManager();
    _vfx.Attach(_events, _view);
    GameUi.BindSimEvents(_events);

    _driver.Attach(_session);
    GameUi.SetLocalPlayerId(_session.LocalPlayerId);
    GameUi.HideResult();
    _session.SetReady(true);
  }

  private void StopSession() {
    SimLog.UnbindStage();
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
    Input.BindFactionCatalog(_factions);
    var crystalScene = GD.Load<PackedScene>("res://Scenes/Objects/Crystal.tscn");
    var turretScene = GD.Load<PackedScene>("res://Scenes/Objects/Turret.tscn");
    var pickupScene = GD.Load<PackedScene>("res://Scenes/Objects/WaterBottle.tscn");
    var oasisScene = GD.Load<PackedScene>("res://Scenes/Objects/Oasis.tscn");
    foreach (var faction in _factions.Entries) {
      TryPrewarm(_pool, faction.HeroScene, _sesCfg.MaxPlayers, $"{faction.DisplayName} hero");
      TryPrewarm(_pool, faction.MinionScene, 64, $"{faction.DisplayName} minion");
    }

    TryPrewarm(_pool, crystalScene, _sesCfg.MaxPlayers, "Crystal");
    TryPrewarm(_pool, turretScene, _sesCfg.MaxPlayers * 2, "Turret");
    TryPrewarm(_pool, pickupScene, 32, "Pickup");
    TryPrewarm(_pool, oasisScene, 4, "Oasis");

    _view = new EntityViewUpdaterNode();
    AddChild(_view);
    Input.BindViewRoot(_view);
  }

  private UnitViewFactory CreateFactory() {
    var crystalScene = GD.Load<PackedScene>("res://Scenes/Objects/Crystal.tscn");
    var turretScene = GD.Load<PackedScene>("res://Scenes/Objects/Turret.tscn");
    var pickupScene = GD.Load<PackedScene>("res://Scenes/Objects/WaterBottle.tscn");
    var oasisScene = GD.Load<PackedScene>("res://Scenes/Objects/Oasis.tscn");
    return new UnitViewFactory(_factions, crystalScene, turretScene, pickupScene, oasisScene, BrokenViewScenes);
  }

  private void OnLocalViewRegistered(EntityViewNode view) {
    _camera?.SetFollowTarget(view);
    var frame = view.Engine?.PredictedFrame.Frame;
    if (frame != null && frame.Has<TeamComponent>(view.EntityRef))
      Input.SetLocalTeamId(frame.GetReadOnly<TeamComponent>(view.EntityRef).TeamId);
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
