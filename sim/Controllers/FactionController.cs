using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim;

public static class FactionController {
  public static bool TrySelect(ref Frame frame, int playerId, int factionId) {
    if (UnitLookup.TryGetPlayerHero(ref frame, playerId, out _)) {
      Reject(ref frame, playerId, factionId, "hero_already_spawned");
      return false;
    }

    var filter = frame.Filter<PlayerFaction>();
    while (filter.Next(out var entity)) {
      ref var slot = ref frame.Get<PlayerFaction>(entity);
      if (slot.PlayerId != playerId)
        continue;

      slot.FactionId = factionId;
      slot.Confirmed = 1;
      return true;
    }

    Reject(ref frame, playerId, factionId, "no_slot_for_player");
    return false;
  }

  private static void Reject(ref Frame frame, int playerId, int factionId, string reason) {
    SimLog.Info(ref frame,
      $"[Faction] REJECT tick={frame.Tick} playerId={playerId} factionId={factionId} reason={reason}");
  }
}
