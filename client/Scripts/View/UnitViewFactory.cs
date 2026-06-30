// Maps renderable unit entities to their PackedScene.

using global::Godot;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Godot;
using Meesles.Avalon.Sim.Models;

namespace Meesles.Avalon {
  public class UnitViewFactory : EntityViewFactory {
    private readonly PackedScene _playerScene;
    private readonly PackedScene _crystalScene;
    private readonly PackedScene _turretScene;
    private readonly PackedScene _minionScene;

    public UnitViewFactory(PackedScene playerScene, PackedScene crystalScene, PackedScene turretScene, PackedScene minionScene) {
      _playerScene = playerScene;
      _crystalScene = crystalScene;
      _turretScene = turretScene;
      _minionScene = minionScene;
    }

    protected override PackedScene ResolvePrefab(Frame frame, EntityRef entity) {
      if (frame.Has<Crystal>(entity)) return _crystalScene;
      if (frame.Has<Turret>(entity)) return _turretScene;
      if (frame.Has<Minion>(entity)) return _minionScene;
      return _playerScene;
    }

    protected override bool ShouldRender(Frame frame, EntityRef entity) {
      return frame.Has<Player>(entity) || frame.Has<Crystal>(entity) || frame.Has<Turret>(entity) || frame.Has<Minion>(entity);
    }
  }
}
