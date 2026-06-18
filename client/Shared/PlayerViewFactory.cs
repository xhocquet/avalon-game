// Maps player entities to the player PackedScene. Mirrors the Unity factory.
using global::Godot;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Godot;

namespace Meesles.Avalon {
  public class PlayerViewFactory : EntityViewFactory {
    private readonly PackedScene _playerScene;
    private readonly PackedScene _baseScene;
    private readonly PackedScene _minionScene;

    public PlayerViewFactory(PackedScene playerScene, PackedScene baseScene, PackedScene minionScene) {
      _playerScene = playerScene;
      _baseScene = baseScene;
      _minionScene = minionScene;
    }

    protected override PackedScene ResolvePrefab(Frame frame, EntityRef entity) {
      if (frame.Has<Base>(entity)) return _baseScene;
      if (frame.Has<Minion>(entity)) return _minionScene;
      return _playerScene;
    }

    protected override bool ShouldRender(Frame frame, EntityRef entity) {
      return frame.Has<PlayerComponent>(entity) || frame.Has<Base>(entity) || frame.Has<Minion>(entity);
    }
  }
}
