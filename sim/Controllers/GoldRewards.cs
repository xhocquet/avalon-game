using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim;

// Deposits kill gold at the kill site, the wallet counterpart to ExperienceRewards. Both are called
// from the same two places (DeathSystem for the board, RespawnSystem for heroes) and share
// MatchStats.IsCreditableKill, so gold, XP and the scoreboard can never disagree about who earned what.
public static class GoldRewards {
  // Paid to whoever landed the fatal hit, and nowhere else. Only heroes carry an Inventory,
  // so a kill credited to a minion or a turret pays out nothing.
  public static void AwardForKill(ref Frame frame, EntityRef killer, int victimUnitTypeId, int victimTeamId) {
    if (!MatchStats.IsCreditableKill(ref frame, killer, victimTeamId) ||
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
