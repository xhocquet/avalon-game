using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim;

public static class GoldController {
  public static void AwardForKill(ref Frame frame, EntityRef killer, int victimUnitTypeId, int victimTeamId) {
    if (!MatchStatsController.IsCreditableKill(ref frame, killer, victimTeamId) ||
        !frame.Has<Inventory>(killer))
      return;

    var gold = GetKillGold(ref frame, victimUnitTypeId);
    if (gold <= 0)
      return;

    frame.Get<Inventory>(killer).Gold += gold;
  }

  private static int GetKillGold(ref Frame frame, int victimUnitTypeId) {
    if (!frame.AssetRegistry.TryGet<GoldRulesAsset>(out var rules))
      return 0;

    return victimUnitTypeId switch {
      SimulationSetup.PlayerUnitTypeId => rules.GoldPerHeroKill,
      SimulationSetup.MinionUnitTypeId => rules.GoldPerMinionKill,
      SimulationSetup.TurretUnitTypeId => rules.GoldPerTurretKill,
      SimulationSetup.CrystalUnitTypeId => rules.GoldPerCrystalKill,
      _ => 0
    };
  }
}
