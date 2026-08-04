using System.Collections.Generic;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim;

// A team is active if it has a champion on the field. Deliberately says nothing about humans, so an
// all-bot team counts the same as a human one. ScoreSystem and TeamPruneSystem both ask this
// question and must agree on the answer.
public static class TeamRegistry {
  public static void CollectActiveTeams(ref Frame frame, List<int> teamIds) {
    teamIds.Clear();

    var filter = frame.Filter<Hero, TeamComponent>();
    while (filter.Next(out var entity))
      AddTeam(teamIds, frame.GetReadOnly<TeamComponent>(entity).TeamId);
  }

  public static void AddTeam(List<int> teamIds, int teamId) {
    if (!teamIds.Contains(teamId))
      teamIds.Add(teamId);
  }
}
