using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using Meesles.Avalon.Sim;
using Meesles.Avalon.Sim.Models;
using Meesles.Avalon.Sim.Assets;
using xpTURN.Klotho.Deterministic.Navigation;

namespace Meesles.Avalon {
  public class RespawnSystem : ISystem {
    public void Update(ref Frame frame) {
      var stats = frame.AssetRegistry.Get<PlayerStatsAsset>();
      var filter = frame.Filter<Player, Team, TransformComponent>();
      while (filter.Next(out var entity)) {
        ref var t = ref frame.Get<TransformComponent>(entity);
        if (t.Position.y >= stats.FallThresholdY) continue;

        ref var p = ref frame.Get<Player>(entity);
        ref readonly var team = ref frame.Get<Team>(entity);
        p.Score -= 1;
        p.LastInputH = FP64.Zero;
        p.LastInputV = FP64.Zero;
        t.Position = SimulationSetup.GetHeroSpawnPositionForTeam(ref frame, team.TeamId);
        if (frame.Has<UnitMoveTarget>(entity))
          frame.Remove<UnitMoveTarget>(entity);
        if (frame.Has<NavAgentComponent>(entity)) {
          ref var nav = ref frame.Get<NavAgentComponent>(entity);
          NavAgentComponent.Stop(ref nav);
          NavAgentComponent.Init(ref nav, t.Position);
        }
      }
    }
  }
}
