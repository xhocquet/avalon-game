using System.Collections.Generic;
using Meesles.Avalon.Sim;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon;

// Spawn, delayed until factions are chosen
public class HeroSpawnSystem : ISystem {
  public void Update(ref Frame frame) {
    // Snapshot the slots we intend to spawn before mutating the frame (SpawnHero creates
    // entities), mirroring WaveSpawnSystem. Filter order is deterministic.
    List<(int PlayerId, int TeamId, int FactionId)> toSpawn = null;
    var graceTicks = SimulationSetup.GetSetupGraceTicks(ref frame);

    var filter = frame.Filter<PlayerFaction>();
    while (filter.Next(out var entity)) {
      ref readonly var slot = ref frame.GetReadOnly<PlayerFaction>(entity);
      if (UnitLookup.TryGetPlayerHero(ref frame, slot.PlayerId, out _))
        continue;
      if (slot.Confirmed == 0 && frame.Tick < graceTicks)
        continue;

      (toSpawn ??= []).Add((slot.PlayerId, slot.TeamId, slot.FactionId));
    }

    if (toSpawn == null)
      return;

    foreach (var s in toSpawn) {
      SimulationSetup.SpawnHero(ref frame, s.PlayerId, s.TeamId, s.FactionId);
    }
  }
}
