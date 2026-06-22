using Godot;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Godot;
using xpTURN.Klotho.LiteNetLib;
using Meesles.Avalon.Client;

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

      InitializeGameUI();
      GameUi.SetMultiplayerMode();
      GameUi.SetPhase(xpTURN.Klotho.Network.SessionPhase.Playing);

      SetupView3D();
      _camera = GetNodeOrNull<CameraController>("Camera3D");
      Input.BindCamera(_camera);

      _viewCallbacks = new ViewCallbacks(GameUi);
      _transport = new LiteNetLibTransport(logger, connectionKey: ConnectionKey);
      _flow = new KlothoSessionFlow(
        new KlothoFlowSetupBuilder((s, ss) =>
            new SessionCallbacks(new SimCallbacks(Input), _viewCallbacks))
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

      _driver.Attach(_session);
      GameUi.SetLocalPlayerId(_session.LocalPlayerId);
      GameUi.HideResult();
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
      var playerScene = GD.Load<PackedScene>("res://Scenes/Objects/Player.tscn");
      var baseScene = GD.Load<PackedScene>("res://Shared/Base.tscn");
      var minionScene = GD.Load<PackedScene>("res://Scenes/Objects/Minion.tscn");
      _pool.Prewarm(playerScene, _sesCfg.MaxPlayers);
      _pool.Prewarm(baseScene, _sesCfg.MaxPlayers);
      _pool.Prewarm(minionScene, 64);

      _view = new EntityViewUpdaterNode();
      AddChild(_view);
      Input.BindViewRoot(_view);
    }

    private UnitViewFactory CreateFactory() {
      var playerScene = GD.Load<PackedScene>("res://Scenes/Objects/Player.tscn");
      var baseScene = GD.Load<PackedScene>("res://Shared/Base.tscn");
      var minionScene = GD.Load<PackedScene>("res://Scenes/Objects/Minion.tscn");
      return new UnitViewFactory(playerScene, baseScene, minionScene);
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
