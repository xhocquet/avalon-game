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
        bool fellOutOfWorld = t.Position.y < stats.FallThresholdY;
        bool died = frame.Has<Health>(entity) && frame.GetReadOnly<Health>(entity).Current <= 0;
        if (!fellOutOfWorld && !died) continue;

        ref var p = ref frame.Get<Player>(entity);
        ref readonly var team = ref frame.Get<Team>(entity);
        p.Score -= 1;
        p.LastInputH = FP64.Zero;
        p.LastInputV = FP64.Zero;
        t.Position = SimulationSetup.GetHeroSpawnPositionForTeam(ref frame, team.TeamId);
        if (frame.Has<UnitMoveTarget>(entity))
          frame.Remove<UnitMoveTarget>(entity);
        if (frame.Has<AttackTargetUnitId>(entity))
          frame.Remove<AttackTargetUnitId>(entity);
        if (frame.Has<Combat>(entity)) {
          ref var combat = ref frame.Get<Combat>(entity);
          combat.Target = default;
          combat.CooldownRemainingTicks = 0;
        }
        if (frame.Has<Health>(entity)) {
          ref var health = ref frame.Get<Health>(entity);
          health.Current = health.Max;
        }
        if (frame.Has<NavAgentComponent>(entity)) {
          ref var nav = ref frame.Get<NavAgentComponent>(entity);
          NavAgentComponent.Stop(ref nav);
          NavAgentComponent.Init(ref nav, t.Position);
        }
      }
    }
  }
}
