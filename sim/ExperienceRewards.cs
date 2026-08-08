using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim;

// Deposits kill XP at the kill site. Two systems own deaths - DeathSystem for everything on the
// board, RespawnSystem for heroes, which never reach DeathSystem - so the "what is a kill worth"
// rule lives here rather than in either of them. ExperienceSystem picks the deposits up later in
// the same tick and turns them into levels.
public static class ExperienceRewards {
  // The XP goes to whoever landed the fatal hit, and nowhere else. Only heroes carry an
  // ExperienceComponent today, so a kill credited to a minion or a turret simply pays out nothing.
  public static void AwardForKill(ref Frame frame, EntityRef killer, int victimUnitTypeId, int victimTeamId) {
    // Nothing was credited with the damage (map damage, a decayed corpse), the killer is gone, or it
    // killed its own - the last of which would otherwise let a team farm its own minions.
    if (!MatchStats.IsCreditableKill(ref frame, killer, victimTeamId) ||
        !frame.Has<ExperienceComponent>(killer))
      return;

    var xp = GetKillXp(ref frame, victimUnitTypeId);
    if (xp <= 0)
      return;

    frame.Get<ExperienceComponent>(killer).Experience += xp;
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
