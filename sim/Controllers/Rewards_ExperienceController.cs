using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim;

public static class ExperienceController {
  public static void AwardForKill(ref Frame frame, EntityRef killer, int victimUnitTypeId, int victimTeamId) {
    // Invalid, friendly, or non-XP-capable killers earn nothing
    if (!MatchStatsController.IsCreditableKill(ref frame, killer, victimTeamId) ||
        !frame.Has<Experience>(killer))
      return;

    var xp = GetKillXp(ref frame, victimUnitTypeId);
    if (xp <= 0)
      return;

    frame.Get<Experience>(killer).Xp += xp;
  }

  private static int GetKillXp(ref Frame frame, int victimUnitTypeId) {
    if (!frame.AssetRegistry.TryGet<XpRulesAsset>(out var rules))
      return 0;

    return victimUnitTypeId switch {
      SimulationSetup.PlayerUnitTypeId => rules.XpPerHeroKill,
      SimulationSetup.MinionUnitTypeId => rules.XpPerMinionKill,
      SimulationSetup.TurretUnitTypeId => rules.XpPerTurretKill,
      SimulationSetup.CrystalUnitTypeId => rules.XpPerCrystalKill,
      _ => 0
    };
  }
}
