using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Logging;

namespace Meesles.Avalon.Sim;

// The rules behind SelectFactionCommand. CommandSystem dispatches straight into these so the command
// layer stays a switch and the rules can be exercised without a wire round-trip.
//
// CommandValidation has already checked that the id names a FactionAsset the registry knows; what is
// left is whether this player still has a pick to make.
public static class FactionActions {
  // Confirm a player's pick onto their PlayerFaction slot. The slot is all this writes — HeroSpawnSystem
  // turns it into a hero once every slot is confirmed or the setup grace period expires, and
  // TeamPruneSystem reads it to keep the team's structures alive until then.
  public static bool TrySelect(ref Frame frame, int playerId, int factionId) {
    // The pick only feeds HeroSpawnSystem. Once the hero exists it is settled, and a later pick would
    // only re-skin the team's minions in the view layer.
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
