using System.Collections.Generic;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim;

public static class UnitLookup {
  public const int FirstUnitId = IdCounter<UnitIdCounter>.FirstId;
  public const int NoPlayerId = -1;

  // Single global sequence for UnitIdentity.UnitId
  public static void InitializeUnitIds(ref Frame frame, int nextUnitId = FirstUnitId) =>
    IdCounter<UnitIdCounter>.Initialize(ref frame, nextUnitId);

  public static int NextUnitId(ref Frame frame) => IdCounter<UnitIdCounter>.Next(ref frame);

  public static bool TryGetEntityByUnitId(ref Frame frame, int unitId, out EntityRef entity) {
    var filter = frame.Filter<UnitIdentity>();
    while (filter.Next(out entity)) {
      ref readonly var unit = ref frame.GetReadOnly<UnitIdentity>(entity);
      if (unit.UnitId == unitId)
        return true;
    }

    entity = default;
    return false;
  }

  // Hero.PlayerId is the single link from a unit back to whoever drives it. Every "find this
  // player's unit" question routes through here so systems don't each pick their own marker and
  // drift apart. Returns the first match: a player owns exactly one hero today.
  public static bool TryGetPlayerHero(ref Frame frame, int playerId, out EntityRef entity) {
    var filter = frame.Filter<Hero>();
    while (filter.Next(out entity)) {
      if (frame.GetReadOnly<Hero>(entity).PlayerId == playerId)
        return true;
    }

    entity = default;
    return false;
  }

  public static bool TryGetPlayerTeamId(ref Frame frame, int playerId, out int teamId) {
    if (TryGetPlayerHero(ref frame, playerId, out var hero) && frame.Has<Team>(hero)) {
      teamId = frame.GetReadOnly<Team>(hero).TeamId;
      return true;
    }

    teamId = 0;
    return false;
  }

  // The player driving this unit, or NoPlayerId when nothing does.
  public static int GetControllerPlayerId(ref Frame frame, EntityRef entity) {
    return entity.IsValid && frame.Has<Hero>(entity)
      ? frame.GetReadOnly<Hero>(entity).PlayerId
      : NoPlayerId;
  }

  // UnitId is optional on an entity, and every caller wants the same fallback.
  public static int GetUnitId(ref Frame frame, EntityRef entity) {
    return entity.IsValid && frame.Has<UnitIdentity>(entity)
      ? frame.GetReadOnly<UnitIdentity>(entity).UnitId
      : 0;
  }

  public static int GetUnitTypeId(ref Frame frame, EntityRef entity) {
    return entity.IsValid && frame.Has<UnitIdentity>(entity)
      ? frame.GetReadOnly<UnitIdentity>(entity).UnitTypeId
      : 0;
  }

  public static bool TryGetTeamUnitById(ref Frame frame, int teamId, int unitId, out EntityRef entity) {
    return TryGetEntityByUnitId(ref frame, unitId, out entity) && KeepIfOnTeam(ref frame, teamId, ref entity);
  }

  public static bool TryGetPlayerOwnedUnitById(ref Frame frame, int playerId, int unitId, out EntityRef entity) {
    if (!TryGetPlayerTeamId(ref frame, playerId, out var teamId)) {
      entity = default;
      return false;
    }

    return TryGetTeamUnitById(ref frame, teamId, unitId, out entity);
  }

  public static bool TryGetPlayerControllableUnitById(ref Frame frame, int playerId, int unitId,
    out EntityRef entity) {
    return TryGetPlayerOwnedUnitById(ref frame, playerId, unitId, out entity) &&
           KeepIfControllable(ref frame, ref entity);
  }

  private static bool KeepIfOnTeam(ref Frame frame, int teamId, ref EntityRef entity) {
    if (frame.Has<Team>(entity) && frame.GetReadOnly<Team>(entity).TeamId == teamId)
      return true;

    entity = default;
    return false;
  }

  private static bool KeepIfControllable(ref Frame frame, ref EntityRef entity) {
    if (frame.Has<Controllable>(entity))
      return true;

    entity = default;
    return false;
  }

  // Rebuild before resolving a batch of ids. Caller-owned, never cached across ticks: a stale index
  // survives a rollback and resolves against a frame that no longer exists.
  public class Index {
    private readonly Dictionary<int, EntityRef> _index = new();

    public void Rebuild(ref Frame frame) {
      _index.Clear();
      var filter = frame.Filter<UnitIdentity>();
      while (filter.Next(out var entity)) {
        ref readonly var unit = ref frame.GetReadOnly<UnitIdentity>(entity);
        _index[unit.UnitId] = entity;
      }
    }

    public bool TryGet(int unitId, out EntityRef entity) {
      return _index.TryGetValue(unitId, out entity);
    }

    public bool TryGetTeamUnitById(ref Frame frame, int teamId, int unitId, out EntityRef entity) {
      return TryGet(unitId, out entity) && KeepIfOnTeam(ref frame, teamId, ref entity);
    }

    public bool TryGetControllableTeamUnitById(ref Frame frame, int teamId, int unitId, out EntityRef entity) {
      return TryGetTeamUnitById(ref frame, teamId, unitId, out entity) && KeepIfControllable(ref frame, ref entity);
    }
  }
}
