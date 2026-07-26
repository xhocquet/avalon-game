using System.Collections.Generic;
using Meesles.Avalon.Sim;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon;

// Spawns each player's hero once their faction pick is known. InitializeWorld seeds one
// PlayerFaction slot per player; a SelectFactionCommand flips Confirmed (and sets FactionId).
// We spawn as soon as the pick is confirmed, or after a grace window using the seeded default
// faction if the pick never arrives (e.g. a disconnected/misbehaving client). Spawning here —
// rather than in InitializeWorld — guarantees the Faction component exists at entity creation,
// which is when the view factory resolves the (faction-specific) scene.
public class HeroSpawnSystem : ISystem {
  public void Update(ref Frame frame) {
    // Snapshot the slots we intend to spawn before mutating the frame (SpawnHero creates
    // entities), mirroring WaveSpawnSystem. Filter order is deterministic.
    List<(int PlayerId, int TeamId, int FactionId)> toSpawn = null;

    var filter = frame.Filter<PlayerFaction>();
    while (filter.Next(out var entity)) {
      ref readonly var slot = ref frame.GetReadOnly<PlayerFaction>(entity);
      if (HasHero(ref frame, slot.PlayerId))
        continue;
      if (slot.Confirmed == 0 && frame.Tick < SimulationSetup.SetupGraceTicks)
        continue;

      (toSpawn ??= new List<(int, int, int)>()).Add((slot.PlayerId, slot.TeamId, slot.FactionId));
    }

    if (toSpawn == null)
      return;

    foreach (var s in toSpawn)
      SimulationSetup.SpawnHero(ref frame, s.PlayerId, s.TeamId, s.FactionId);
  }

  private static bool HasHero(ref Frame frame, int playerId) {
    var filter = frame.Filter<Hero>();
    while (filter.Next(out var entity)) {
      ref readonly var hero = ref frame.GetReadOnly<Hero>(entity);
      if (hero.PlayerId == playerId)
        return true;
    }

    return false;
  }
}
