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

  // Faction ids already reported by ResolvePrefab. Reconcile runs every tick and retries every entity
  // it could not spawn, so without this one mis-factioned unit writes a stack trace per tick forever.
  private readonly HashSet<int> _reportedFactionIds = [];

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
    PackedScene scene;
    try {
      scene = ResolveScene(frame, entity);
    }
    catch (KeyNotFoundException) {
      // Still not rendered - the unit is mis-configured and a placeholder would hide that. Reported
      // once instead of thrown per tick, so the log stays readable and the frame rate survives.
      ReportUnresolvedFaction(frame, entity);
      return null;
    }

    // Null is the framework's documented "skip this entity" path; the prewarm probe already logged why.
    return _brokenScenes != null && _brokenScenes.Contains(scene) ? null : scene;
  }

  private void ReportUnresolvedFaction(Frame frame, EntityRef entity) {
    var factionId = ResolveFactionId(frame, entity);
    if (!_reportedFactionIds.Add(factionId))
      return;

    var teamId = frame.Has<Team>(entity) ? frame.GetReadOnly<Team>(entity).TeamId : 0;
    GD.PushError(
      $"[View] No faction registered for id {factionId} (team {teamId}) — those units will not render. " +
      "A unit whose team has no PlayerFaction slot needs a Faction of its own.");
  }

  private PackedScene ResolveScene(Frame frame, EntityRef entity) {
    if (frame.Has<Crystal>(entity)) return _crystalScene;
    if (frame.Has<Turret>(entity)) return _turretScene;
    if (frame.Has<Pickup>(entity)) return _pickupScene;
    if (frame.Has<Oasis>(entity)) return _oasisScene;

    var entry = _factions.Resolve(ResolveFactionId(frame, entity));
    return frame.Has<Minion>(entity) ? entry.MinionScene : entry.HeroScene;
  }

  // Heroes carry Faction directly; other faction-aligned units (minions) inherit it from their
  // team's pick. Returns 0 when neither is available — an id the catalog doesn't register, so
  // Resolve throws and surfaces the mis-configured unit rather than rendering a placeholder.
  private static int ResolveFactionId(Frame frame, EntityRef entity) {
    if (frame.Has<Faction>(entity))
      return frame.GetReadOnly<Faction>(entity).FactionId;

    if (frame.Has<Team>(entity)) {
      var teamId = frame.GetReadOnly<Team>(entity).TeamId;
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
    if (!view.TryGetCachedUnitId(out var cachedUnitId) || !frame.Has<UnitIdentity>(entity))
      return true;

    return cachedUnitId == frame.GetReadOnly<UnitIdentity>(entity).UnitId;
  }

  protected override bool ShouldRender(Frame frame, EntityRef entity) {
    return frame.Has<Hero>(entity) || frame.Has<Crystal>(entity) || frame.Has<Turret>(entity) ||
           frame.Has<Minion>(entity) || frame.Has<Pickup>(entity) || frame.Has<Oasis>(entity);
  }
}
