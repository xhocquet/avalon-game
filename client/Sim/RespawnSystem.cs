using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon {
  public class RespawnSystem : ISystem {
    public void Update(ref Frame frame) {
      var stats = frame.AssetRegistry.Get<PlayerStatsAsset>();
      var filter = frame.Filter<PlayerComponent, Team, TransformComponent>();
      while (filter.Next(out var entity)) {
        ref var t = ref frame.Get<TransformComponent>(entity);
        if (t.Position.y >= stats.FallThresholdY) continue;

        ref var p = ref frame.Get<PlayerComponent>(entity);
        ref readonly var team = ref frame.Get<Team>(entity);
        p.Score -= 1;
        p.LastInputH = FP64.Zero;
        p.LastInputV = FP64.Zero;
        t.Position = SimulationSetup.GetHeroSpawnPositionForTeam(ref frame, team.TeamId);
      }
    }
  }
}
