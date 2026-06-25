using Meesles.Avalon.Sim.Models;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim {
  public static class UnitLookup {
    public static bool TryGetEntityByUnitId(ref Frame frame, int unitId, out EntityRef entity) {
      var filter = frame.Filter<Unit>();
      while (filter.Next(out entity)) {
        ref readonly var unit = ref frame.GetReadOnly<Unit>(entity);
        if (unit.UnitId == unitId)
          return true;
      }

      entity = default;
      return false;
    }

    public static bool TryGetPlayerTeamId(ref Frame frame, int playerId, out int teamId) {
      var filter = frame.Filter<Player, Team>();
      while (filter.Next(out var entity)) {
        ref readonly var player = ref frame.GetReadOnly<Player>(entity);
        if (player.PlayerId != playerId)
          continue;

        teamId = frame.GetReadOnly<Team>(entity).TeamId;
        return true;
      }

      teamId = 0;
      return false;
    }

    public static bool TryGetTeamUnitById(ref Frame frame, int teamId, int unitId, out EntityRef entity) {
      if (!TryGetEntityByUnitId(ref frame, unitId, out entity))
        return false;

      if (!frame.Has<Team>(entity)) {
        entity = default;
        return false;
      }

      ref readonly var team = ref frame.GetReadOnly<Team>(entity);
      if (team.TeamId != teamId) {
        entity = default;
        return false;
      }

      return true;
    }

    public static bool TryGetPlayerOwnedUnitById(ref Frame frame, int playerId, int unitId, out EntityRef entity) {
      if (!TryGetPlayerTeamId(ref frame, playerId, out int teamId)) {
        entity = default;
        return false;
      }

      return TryGetTeamUnitById(ref frame, teamId, unitId, out entity);
    }
  }
}
