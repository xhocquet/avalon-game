using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using Meesles.Avalon.Sim;
using Meesles.Avalon.Sim.Models;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Navigation;

namespace Meesles.Avalon {
  public class RespawnSystem : ISystem {
    private const int RespawnDelayMs = 5000;

    public void Update(ref Frame frame) {
      var filter = frame.Filter<Player, Team, Unit, TransformComponent, Health>();
      while (filter.Next(out var entity)) {
        if (frame.Has<PendingRespawn>(entity)) {
          ref readonly var transform = ref frame.GetReadOnly<TransformComponent>(entity);
          ClearActiveState(ref frame, entity, transform.Position);

          ref var pending = ref frame.Get<PendingRespawn>(entity);
          if (pending.RemainingTicks > 0)
            pending.RemainingTicks--;

          if (pending.RemainingTicks <= 0)
            CompleteRespawn(ref frame, entity);

          continue;
        }

        ref readonly var health = ref frame.GetReadOnly<Health>(entity);
        if (health.Current <= 0)
          BeginRespawn(ref frame, entity);
      }
    }

    private static void BeginRespawn(ref Frame frame, EntityRef entity) {
      int delayTicks = GetRespawnDelayTicks(ref frame);
      frame.Add(entity, new PendingRespawn { RemainingTicks = delayTicks });

      ref var player = ref frame.Get<Player>(entity);
      ref readonly var team = ref frame.GetReadOnly<Team>(entity);
      ref readonly var unit = ref frame.GetReadOnly<Unit>(entity);
      ref readonly var transform = ref frame.GetReadOnly<TransformComponent>(entity);

      player.Score -= 1;
      player.LastInputH = FP64.Zero;
      player.LastInputV = FP64.Zero;
      ClearActiveState(ref frame, entity, transform.Position);

      if (frame.EventRaiser != null) {
        var evt = EventPool.Get<PlayerDiedEvent>();
        evt.PlayerId = player.PlayerId;
        evt.TeamId = team.TeamId;
        evt.UnitId = unit.UnitId;
        evt.Position = transform.Position;
        evt.RespawnDelayTicks = delayTicks;
        frame.EventRaiser.RaiseEvent(evt);
      }
    }

    private static void CompleteRespawn(ref Frame frame, EntityRef entity) {
      ref readonly var player = ref frame.GetReadOnly<Player>(entity);
      ref readonly var team = ref frame.GetReadOnly<Team>(entity);
      ref readonly var unit = ref frame.GetReadOnly<Unit>(entity);
      ref var transform = ref frame.Get<TransformComponent>(entity);
      ref var health = ref frame.Get<Health>(entity);

      transform.Position = SimulationSetup.GetHeroSpawnPositionForTeam(ref frame, team.TeamId);
      health.Current = health.Max;
      frame.Remove<PendingRespawn>(entity);
      ClearActiveState(ref frame, entity, transform.Position);

      if (frame.EventRaiser != null) {
        var evt = EventPool.Get<PlayerRespawnedEvent>();
        evt.PlayerId = player.PlayerId;
        evt.TeamId = team.TeamId;
        evt.UnitId = unit.UnitId;
        evt.Position = transform.Position;
        frame.EventRaiser.RaiseEvent(evt);
      }
    }

    private static void ClearActiveState(ref Frame frame, EntityRef entity, FPVector3 navPosition) {
      if (frame.Has<UnitMoveTarget>(entity))
        frame.Remove<UnitMoveTarget>(entity);
      if (frame.Has<AttackTargetUnitId>(entity))
        frame.Remove<AttackTargetUnitId>(entity);
      if (frame.Has<Combat>(entity)) {
        ref var combat = ref frame.Get<Combat>(entity);
        combat.Target = default;
        combat.CooldownRemainingTicks = 0;
      }
      if (frame.Has<NavAgentComponent>(entity)) {
        ref var nav = ref frame.Get<NavAgentComponent>(entity);
        NavAgentComponent.Stop(ref nav);
        NavAgentComponent.Init(ref nav, navPosition);
      }
    }

    private static int GetRespawnDelayTicks(ref Frame frame) {
      int deltaTimeMs = frame.DeltaTimeMs > 0 ? frame.DeltaTimeMs : 16;
      return (RespawnDelayMs + deltaTimeMs - 1) / deltaTimeMs;
    }
  }
}
