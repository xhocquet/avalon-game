using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim;

public static class RewardNotifications {
  public static void AwardKill(ref Frame frame, EntityRef killer, int victimTypeId, int victimTeamId, FPVector3 position) {
    var goldBefore = frame.Has<Inventory>(killer) ? frame.GetReadOnly<Inventory>(killer).Gold : 0;
    var xpBefore = frame.Has<Experience>(killer) ? frame.GetReadOnly<Experience>(killer).Xp : 0;
    GoldController.AwardForKill(ref frame, killer, victimTypeId, victimTeamId);
    ExperienceController.AwardForKill(ref frame, killer, victimTypeId, victimTeamId);
    var gold = frame.Has<Inventory>(killer) ? frame.GetReadOnly<Inventory>(killer).Gold - goldBefore : 0;
    var xp = frame.Has<Experience>(killer) ? frame.GetReadOnly<Experience>(killer).Xp - xpBefore : 0;
    if (gold > 0 || xp > 0) Raise(ref frame, killer, position, gold, xp);
  }

  public static void Raise(ref Frame frame, EntityRef recipient, FPVector3 position,
    int gold = 0, int xp = 0, int resourceTypeId = 0, int resourceAmount = 0) {
    if (frame.EventRaiser == null || !frame.Has<Team>(recipient)) return;
    var evt = EventPool.Get<RewardGainedEvent>();
    evt.TeamId = frame.GetReadOnly<Team>(recipient).TeamId;
    evt.Gold = gold;
    evt.Xp = xp;
    evt.ResourceTypeId = resourceTypeId;
    evt.ResourceAmount = resourceAmount;
    evt.Position = position;
    frame.EventRaiser.RaiseEvent(evt);
  }
}
