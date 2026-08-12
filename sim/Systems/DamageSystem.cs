using Meesles.Avalon.Sim;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Logging;

namespace Meesles.Avalon;

public class DamageSystem : ISystem {
  private readonly UnitLookup.Index _unitIdIndex = new();

  public void Update(ref Frame frame) {
    _unitIdIndex.Rebuild(ref frame);

    var filter = frame.Filter<Combat, TeamComponent, AttackTargetUnitId>();
    while (filter.Next(out var attacker)) {
      ref var combat = ref frame.Get<Combat>(attacker);
      if (combat.CooldownRemainingTicks > 0)
        continue;

      if (combat.TargetUnitId == 0)
        continue;

      if (!_unitIdIndex.TryGet(combat.TargetUnitId, out var target) ||
          !CombatTargeting.IsHostileAndAlive(ref frame, attacker, target)) {
        LogDamageState(ref frame, attacker, combat.TargetUnitId, "invalid_damage_target");
        combat.TargetUnitId = 0;
        continue;
      }

      var healthBefore = frame.GetReadOnly<Health>(target).Current;
      var damage = DamageApplication.ApplyDamage(ref frame, attacker, target,
        GetAttackDamage(ref frame, attacker), DamageType.Physical, canCrit: true);

      combat.CooldownRemainingTicks = CombatTiming.CooldownTicks(ref frame, attacker);

      LogDamageState(ref frame, attacker, combat.TargetUnitId,
        $"damage={damage} health={healthBefore}->{frame.GetReadOnly<Health>(target).Current} cooldown={combat.CooldownRemainingTicks}");
    }
  }

  // Attackers without a StatsComponent block (nothing today, but structures/summons may skip it) deal nothing.
  private static FP64 GetAttackDamage(ref Frame frame, EntityRef attacker) {
    return frame.Has<StatsComponent>(attacker)
      ? frame.GetReadOnly<StatsComponent>(attacker).AttackDamage
      : FP64.Zero;
  }

  private static void LogDamageState(ref Frame frame, EntityRef attacker, int targetUnitId, string state) {
    frame.Logger.KDebug(
      $"[Combat] DamageSystem tick={frame.Tick} sourceUnitId={UnitLookup.GetUnitId(ref frame, attacker)} " +
      $"targetUnitId={targetUnitId} state={state}");
  }
}
