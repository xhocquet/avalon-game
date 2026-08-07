using Meesles.Avalon.Sim;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.Deterministic.Navigation;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon;

public class RespawnSystem : ISystem {
  public void Update(ref Frame frame) {
    var filter = frame.Filter<Respawns, TeamComponent, UnitIdComponent, TransformComponent, Health>();
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

      if (!frame.GetReadOnly<Health>(entity).IsAlive)
        BeginRespawn(ref frame, entity);
    }
  }

  private static void BeginRespawn(ref Frame frame, EntityRef entity) {
    // Respawning units never reach DeathSystem, so the kill credit for one is settled here.
    AwardKillExperience(ref frame, entity);

    var rules = frame.AssetRegistry.Get<MatchRulesAsset>();
    var delayTicks = GetRespawnDelayTicks(ref frame, rules);
    frame.Add(entity, new PendingRespawn { RemainingTicks = delayTicks });

    ref readonly var team = ref frame.GetReadOnly<TeamComponent>(entity);
    ref readonly var unit = ref frame.GetReadOnly<UnitIdComponent>(entity);
    ref readonly var transform = ref frame.GetReadOnly<TransformComponent>(entity);

    // The score penalty lands on the human's record; a unit nobody is scoring for simply skips it.
    if (frame.Has<Player>(entity))
      frame.Get<Player>(entity).Score -= rules?.DeathScorePenalty ?? 0;

    ClearActiveState(ref frame, entity, transform.Position);

    if (frame.EventRaiser != null) {
      var evt = EventPool.Get<PlayerDiedEvent>();
      evt.PlayerId = UnitLookup.GetControllerPlayerId(ref frame, entity);
      evt.TeamId = team.TeamId;
      evt.UnitId = unit.UnitId;
      evt.Position = transform.Position;
      evt.RespawnDelayTicks = delayTicks;
      frame.EventRaiser.RaiseEvent(evt);
    }
  }

  private static void AwardKillExperience(ref Frame frame, EntityRef entity) {
    var lastDamagerUnitId = frame.GetReadOnly<Health>(entity).LastDamagerUnitId;
    if (lastDamagerUnitId == 0 || !UnitLookup.TryGetEntityByUnitId(ref frame, lastDamagerUnitId, out var killer))
      return;

    var victimTeamId = frame.GetReadOnly<TeamComponent>(entity).TeamId;
    ExperienceRewards.AwardForKill(ref frame, killer, SimulationSetup.PlayerUnitTypeId, victimTeamId);
  }

  private static void CompleteRespawn(ref Frame frame, EntityRef entity) {
    ref readonly var team = ref frame.GetReadOnly<TeamComponent>(entity);
    ref readonly var unit = ref frame.GetReadOnly<UnitIdComponent>(entity);
    ref var transform = ref frame.Get<TransformComponent>(entity);
    ref var health = ref frame.Get<Health>(entity);

    transform.Position = SimulationSetup.GetHeroSpawnPositionForTeam(ref frame, team.TeamId);
    health.Current = frame.GetReadOnly<StatsComponent>(entity).MaxHealth;
    health.LastDamagerUnitId = 0;
    frame.Remove<PendingRespawn>(entity);
    ClearActiveState(ref frame, entity, transform.Position);

    if (frame.EventRaiser != null) {
      var evt = EventPool.Get<PlayerRespawnedEvent>();
      evt.PlayerId = UnitLookup.GetControllerPlayerId(ref frame, entity);
      evt.TeamId = team.TeamId;
      evt.UnitId = unit.UnitId;
      evt.Position = transform.Position;
      frame.EventRaiser.RaiseEvent(evt);
    }
  }

  private static void ClearActiveState(ref Frame frame, EntityRef entity, FPVector3 navPosition) {
    UnitIntent.ClearMoveTarget(ref frame, entity);
    UnitIntent.ClearAttackIntent(ref frame, entity);
    // Not part of the shared intent reset: a respawn also refunds the in-flight attack cooldown.
    if (frame.Has<Combat>(entity)) {
      ref var combat = ref frame.Get<Combat>(entity);
      combat.CooldownRemainingTicks = 0;
    }

    if (frame.Has<NavAgentComponent>(entity)) {
      ref var nav = ref frame.Get<NavAgentComponent>(entity);
      // Init resets Radius/Speed/Acceleration to component defaults; preserve the values this
      // agent was configured with at spawn so respawned units keep their tuned footprint/speed.
      var radius = nav.Radius;
      var speed = nav.Speed;
      var acceleration = nav.Acceleration;
      NavAgentComponent.Stop(ref nav);
      NavAgentComponent.Init(ref nav, navPosition);
      nav.Radius = radius;
      nav.Speed = speed;
      nav.Acceleration = acceleration;
    }
  }

  private static int GetRespawnDelayTicks(ref Frame frame, MatchRulesAsset rules) {
    return TickMath.MsToTicksCeil(ref frame, rules?.RespawnDelayMs ?? 0);
  }
}
