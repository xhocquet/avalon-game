using System.Collections.Generic;
using Godot;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Godot;

namespace Meesles.Avalon.Client.Scripts.View;

public class UnitViewFactory : EntityViewFactory {
  private readonly PackedScene _crystalScene;
  private readonly FactionCatalog _factions;
  private readonly PackedScene _turretScene;
  private readonly PackedScene _pickupScene;
  private readonly PackedScene _oasisScene;
  private readonly IReadOnlySet<PackedScene> _brokenScenes;

  public UnitViewFactory(FactionCatalog factions, PackedScene crystalScene, PackedScene turretScene,
    PackedScene pickupScene = null, PackedScene oasisScene = null,
    IReadOnlySet<PackedScene> brokenScenes = null) {
    _factions = factions;
    _crystalScene = crystalScene;
    _turretScene = turretScene;
    _pickupScene = pickupScene;
    _oasisScene = oasisScene;
    _brokenScenes = brokenScenes;
  }

  protected override PackedScene ResolvePrefab(Frame frame, EntityRef entity) {
    var scene = ResolveScene(frame, entity);
    // Null is the framework's documented "skip this entity" path; the prewarm probe already logged why.
    return _brokenScenes != null && _brokenScenes.Contains(scene) ? null : scene;
  }

  private PackedScene ResolveScene(Frame frame, EntityRef entity) {
    if (frame.Has<Crystal>(entity)) return _crystalScene;
    if (frame.Has<Turret>(entity)) return _turretScene;
    if (frame.Has<Pickup>(entity)) return _pickupScene;
    if (frame.Has<Oasis>(entity)) return _oasisScene;

    var entry = _factions.Resolve(ResolveFactionId(frame, entity));
    return frame.Has<Minion>(entity) ? entry.MinionScene : entry.HeroScene;
  }

  // Heroes carry FactionComponent directly; other faction-aligned units (minions) inherit it from their
  // team's pick. Returns 0 when neither is available — an id the catalog doesn't register, so
  // Resolve throws and surfaces the mis-configured unit rather than rendering a placeholder.
  private static int ResolveFactionId(Frame frame, EntityRef entity) {
    if (frame.Has<FactionComponent>(entity))
      return frame.GetReadOnly<FactionComponent>(entity).FactionId;

    if (frame.Has<TeamComponent>(entity)) {
      var teamId = frame.GetReadOnly<TeamComponent>(entity).TeamId;
      var filter = frame.Filter<PlayerFaction>();
      while (filter.Next(out var slot)) {
        ref readonly var pf = ref frame.GetReadOnly<PlayerFaction>(slot);
        if (pf.TeamId == teamId)
          return pf.FactionId;
      }
    }

    return 0;
  }

  // A recycled index can land another unit of the same faction, which resolves the same prefab and so
  // slips past the prefab check. UnitId comes from the monotonic counter and separates the two. Only
  // rejects when both sides carry an id, so views that track none never churn.
  public override bool IsSameEntity(Frame frame, EntityRef entity, EntityViewNode view) {
    if (!view.TryGetCachedUnitId(out var cachedUnitId) || !frame.Has<UnitIdComponent>(entity))
      return true;

    return cachedUnitId == frame.GetReadOnly<UnitIdComponent>(entity).UnitId;
  }

  protected override bool ShouldRender(Frame frame, EntityRef entity) {
    return frame.Has<Hero>(entity) || frame.Has<Crystal>(entity) || frame.Has<Turret>(entity) ||
           frame.Has<Minion>(entity) || frame.Has<Pickup>(entity) || frame.Has<Oasis>(entity);
  }
}
