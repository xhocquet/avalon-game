using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using Meesles.Avalon.Client.Sim.Models;

namespace Meesles.Avalon {
  // Applies melee combat damage each tick. Generic over unit type: any entity with
  // Combat + Team + TransformComponent attacks the nearest enemy with Health + Team.
  // Distance is planar (x/z only); y is not gameplay.
  public class CombatSystem : ISystem {
    public void Update(ref Frame frame) {
      var attackerFilter = frame.Filter<Combat, Team, TransformComponent>();
      while (attackerFilter.Next(out var attacker)) {
        ref var combat = ref frame.Get<Combat>(attacker);

        if (combat.CooldownRemainingTicks > 0) {
          combat.CooldownRemainingTicks--;
          continue;
        }

        ref readonly var attackerTeam = ref frame.GetReadOnly<Team>(attacker);
        ref readonly var attackerPos = ref frame.GetReadOnly<TransformComponent>(attacker);

        bool foundTarget = false;
        FP64 bestDistSq = FP64.Zero;
        EntityRef bestTarget = default;

        var targetFilter = frame.Filter<Health, Team, TransformComponent>();
        while (targetFilter.Next(out var target)) {
          if (target.Index == attacker.Index) continue;

          ref readonly var targetTeam = ref frame.GetReadOnly<Team>(target);
          if (targetTeam.TeamId == attackerTeam.TeamId) continue;

          ref readonly var targetHealth = ref frame.GetReadOnly<Health>(target);
          if (targetHealth.Current <= 0) continue;

          ref readonly var targetPos = ref frame.GetReadOnly<TransformComponent>(target);
          FPVector3 diff = targetPos.Position - attackerPos.Position;
          FP64 distSq = diff.x * diff.x + diff.z * diff.z;

          if (!foundTarget || distSq < bestDistSq) {
            foundTarget = true;
            bestDistSq = distSq;
            bestTarget = target;
          }
        }

        if (!foundTarget) continue;

        FP64 rangeSq = combat.AttackRange * combat.AttackRange;
        if (bestDistSq > rangeSq) continue;

        ref var health = ref frame.Get<Health>(bestTarget);
        health.Current -= combat.AttackDamage;
        if (health.Current < 0) health.Current = 0;

        combat.CooldownRemainingTicks = combat.AttackCooldownTicks;
      }
    }
  }
}
