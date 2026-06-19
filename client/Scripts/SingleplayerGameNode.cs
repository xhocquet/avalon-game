using global::Godot;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Godot;
using xpTURN.Klotho.LiteNetLib;

namespace Meesles.Avalon {
  public partial class SingleplayerGameNode : GameNode {
    private const string ConnectionKey = "Meesles.Avalon.Singleplayer";

    private LiteNetLibTransport _transport;
    private KlothoSessionFlow _flow;
    private KlothoSession _session;
    private ViewCallbacks _viewCallbacks;
    private EntityViewUpdaterNode _view;
    private DefaultGodotEntityViewPool _pool;
    private GodotSessionDriver _driver;
    private CameraController _camera;
    private ISimulationConfig _simCfg;
    private ISessionConfig _sesCfg;

    public override void _Ready() {
      WarmupRegistry.RunAll();

      var logger = CreateLogger("Singleplayer");
      var registry = LoadAssetRegistry();
      _simCfg = new SimulationConfig();
      _sesCfg = new SessionConfig { MaxPlayers = 1, MinPlayers = 1, CountdownDurationMs = 0 };

      InitializeSharedNodes();
      Menu.SetSingleplayerMode();
      Menu.OnResetClicked += ResetSession;
      Hud.SetMultiplayerMode();
      Hud.SetPhase(xpTURN.Klotho.Network.SessionPhase.Playing);

      SetupView3D();
      _camera = GetNodeOrNull<CameraController>("Camera3D");
      Input.BindCamera(_camera);

      _viewCallbacks = new ViewCallbacks(Hud);
      _transport = new LiteNetLibTransport(logger, connectionKey: ConnectionKey);
      _flow = new KlothoSessionFlow(
          new KlothoFlowSetupBuilder((s, ss) =>
                  new SessionCallbacks(new SimulationCallbacks(Input), _viewCallbacks))
              .WithLogger(logger)
              .WithTransport(_transport)
              .WithAssetRegistry(registry)
              .WithGodotDefaults()
              .Build()
      );

      _driver = new GodotSessionDriver();
      AddChild(_driver);
      _driver.BindTransport(_transport);
      _driver.PreSessionUpdate += (s, dt) => { if (s.State == KlothoState.Running) Input.CaptureInput(); };

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

      _driver.Attach(_session);
      Hud.SetLocalPlayerId(_session.LocalPlayerId);
      Hud.HideResult();
      _session.SetReady(true);
    }

    private void StopSession() {
      UnbindCameraFollow();
      _driver?.DetachAndStop(saveReplay: false);
      _view?.Cleanup();
      _session = null;
    }

    private void CreateView() {
      _pool = new DefaultGodotEntityViewPool();
      var playerScene = GD.Load<PackedScene>("res://Shared/Player.tscn");
      var baseScene = GD.Load<PackedScene>("res://Shared/Base.tscn");
      var minionScene = GD.Load<PackedScene>("res://Shared/Minion.tscn");
      _pool.Prewarm(playerScene, _sesCfg.MaxPlayers);
      _pool.Prewarm(baseScene, _sesCfg.MaxPlayers);
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

    public override void _ExitTree() {
      StopSession();
      _pool?.Dispose();
      _viewCallbacks?.Cleanup();
      base._ExitTree();
    }
  }
}
