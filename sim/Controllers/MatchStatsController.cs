using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim;

public static class MatchStatsController {
  public static bool IsCreditableKill(ref Frame frame, EntityRef killer, int victimTeamId) {
    if (!killer.IsValid)
      return false;

    return !frame.Has<Team>(killer) || frame.GetReadOnly<Team>(killer).TeamId != victimTeamId;
  }

  public static void RecordKill(ref Frame frame, EntityRef killer, int victimUnitTypeId, int victimTeamId) {
    if (!IsCreditableKill(ref frame, killer, victimTeamId) || !frame.Has<Player>(killer))
      return;

    var rules = frame.AssetRegistry.Get<MatchRulesAsset>();
    ref var record = ref frame.Get<Player>(killer);

    switch (victimUnitTypeId) {
      case SimulationSetup.PlayerUnitTypeId:
        record.HeroKills++;
        record.Score += rules?.HeroKillScore ?? 0;
        break;
      case SimulationSetup.MinionUnitTypeId:
        record.MinionKills++;
        record.Score += rules?.MinionKillScore ?? 0;
        break;
      case SimulationSetup.TurretUnitTypeId:
      case SimulationSetup.CrystalUnitTypeId:
        record.StructureKills++;
        record.Score += rules?.StructureKillScore ?? 0;
        break;
    }
  }

  public static void RecordDeath(ref Frame frame, EntityRef victim) {
    if (!frame.Has<Player>(victim))
      return;

    var rules = frame.AssetRegistry.Get<MatchRulesAsset>();
    ref var record = ref frame.Get<Player>(victim);
    record.Deaths++;
    record.Score -= rules?.DeathScorePenalty ?? 0;
  }

  public static void RecordDamage(ref Frame frame, EntityRef source, EntityRef target, FP64 damage) {
    if (damage <= FP64.Zero || !frame.Has<Player>(source))
      return;

    var targetTeamId = frame.Has<Team>(target) ? frame.GetReadOnly<Team>(target).TeamId : 0;
    if (!IsCreditableKill(ref frame, source, targetTeamId))
      return;

    frame.Get<Player>(source).DamageDealt += damage;
  }
}
