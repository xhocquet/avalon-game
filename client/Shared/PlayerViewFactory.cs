// Maps player entities to the player PackedScene. Mirrors the Unity factory.
using global::Godot;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Godot;

namespace Meesles.Avalon {
  public class PlayerViewFactory : EntityViewFactory {
    private readonly PackedScene _playerScene;
    private readonly PackedScene _baseScene;

    public PlayerViewFactory(PackedScene playerScene, PackedScene baseScene) {
      _playerScene = playerScene;
      _baseScene = baseScene;
    }

    protected override PackedScene ResolvePrefab(Frame frame, EntityRef entity) {
      if (frame.Has<Base>(entity)) return _baseScene;
      return _playerScene;
    }

    protected override bool ShouldRender(Frame frame, EntityRef entity) {
      return frame.Has<PlayerComponent>(entity) || frame.Has<Base>(entity);
    }
  }
}
