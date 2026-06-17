// Maps player entities to the player PackedScene. Mirrors the Unity factory.
using global::Godot;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Godot;

namespace Meesles.Avalon
{
  public class PlayerViewFactory : EntityViewFactory
  {
    private readonly PackedScene _playerScene;

    public PlayerViewFactory(PackedScene playerScene)
    {
      _playerScene = playerScene;
    }

    protected override PackedScene ResolvePrefab(Frame frame, EntityRef entity)
    {
      return _playerScene;
    }

    protected override bool ShouldRender(Frame frame, EntityRef entity)
    {
      return frame.Has<PlayerComponent>(entity);
    }
  }
}
