using System.Collections.Generic;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using Meesles.Avalon.Sim.Factories;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon;

public class WaveSpawnSystem : ISystem {
  public void Update(ref Frame frame) {
    var rules = frame.AssetRegistry.Get<WaveRulesAsset>();
    var stats = frame.AssetRegistry.Get<MinionStatsAsset>();
    if (rules.SpawnIntervalTicks <= 0 || rules.MinionsPerWave <= 0) return;

    var rel = frame.Tick - rules.FirstWaveDelayTicks;
    if (rel < 0 || rel % rules.SpawnIntervalTicks != 0) return;
    var waveId = rel / rules.SpawnIntervalTicks;

    // Snapshot spawn points before creating entities so we don't mutate the set
    // we're iterating. Filter order is deterministic, so this stays in sync.
    var sources = new List<(FPVector3 Position, int TeamId)>();
    var filter = frame.Filter<SpawnPoint, Team, TransformComponent>();
    while (filter.Next(out var entity)) {
      ref readonly var team = ref frame.Get<Team>(entity);
      ref readonly var transform = ref frame.Get<TransformComponent>(entity);
      sources.Add((transform.Position, team.TeamId));
    }

    foreach (var source in sources)
      SpawnWave(ref frame, rules, stats, source.Position, source.TeamId, waveId);
  }

  private static void SpawnWave(ref Frame frame, WaveRulesAsset rules, MinionStatsAsset stats, FPVector3 origin,
    int teamId, int waveId) {
    var count = rules.MinionsPerWave;

    for (var i = 0; i < count; i++) {
      var slotIndex = GetFirstFreeSlot(ref frame, origin, teamId, rules.MinionSpacing);
      var position = GetSpawnPosition(origin, rules.MinionSpacing, slotIndex);
      MinionFactory.Spawn(ref frame, stats, position, GetSpawnFacing(origin, position), teamId, waveId);
    }
  }

  private static int GetFirstFreeSlot(ref Frame frame, FPVector3 origin, int teamId, FP64 spacing) {
    var slot = 0;
    while (IsSlotOccupied(ref frame, origin, teamId, spacing, slot))
      slot++;

    return slot;
  }

  private static bool IsSlotOccupied(ref Frame frame, FPVector3 origin, int teamId, FP64 spacing, int slot) {
    var slotPosition = GetSpawnPosition(origin, spacing, slot);
    var occupiedRadius = spacing * FP64.Half;
    var occupiedRadiusSqr = occupiedRadius * occupiedRadius;

    var filter = frame.Filter<Minion, Team, TransformComponent>();
    while (filter.Next(out var entity)) {
      ref readonly var team = ref frame.Get<Team>(entity);
      if (team.TeamId != teamId)
        continue;

      ref readonly var transform = ref frame.Get<TransformComponent>(entity);
      if ((transform.Position - slotPosition).sqrMagnitude <= occupiedRadiusSqr)
        return true;
    }

    return false;
  }

  // Compact hex-packed cluster centred on the spawn point. Ring k holds 6k slots at radius
  // k*spacing; slot 0 is the centre. This packs minions as tightly as they settle when moving,
  // instead of the old wide 90° fan that sprawled across the base. The occupancy-based free-slot
  // search fills innermost-first and reuses slots as minions march off.
  //
  // Ring slots are laid out from a fixed world basis (+Z, so slot 0 of each ring is at angle 0).
  // This used to derive the basis from the direction to the world origin and push the whole
  // cluster two spacings along it — "toward the lane" defined as "toward (0,0)". That happens to
  // hold for the current symmetric map and is silently wrong for any map not centred there, so
  // the cluster now sits on the spawn point and grows outward from it. The starting angle of a
  // rotationally symmetric ring carries no meaning, so a fixed basis costs nothing.
  private static FPVector3 GetSpawnPosition(FPVector3 origin, FP64 spacing, int index) {
    if (index <= 0)
      return origin;

    var ring = 1;
    var ringStart = 1;
    var capacity = 6;
    while (index >= ringStart + capacity) {
      ringStart += capacity;
      ring++;
      capacity = 6 * ring;
    }

    var slot = index - ringStart;
    var radius = spacing * FP64.FromInt(ring);
    var angle = FP64.TwoPi / FP64.FromInt(capacity) * FP64.FromInt(slot);
    var offset = new FPVector3(FP64.Sin(angle), FP64.Zero, FP64.Cos(angle)) * radius;
    return origin + offset;
  }

  // Minions spawn facing away from the spawn point they came out of, so a wave fans outward
  // instead of every slot staring down +Z until NavigationAgentSystem overwrites Rotation from
  // velocity on the first tick they move. Same Atan2(x, z) yaw convention as that system and
  // CommandSystem. Slot 0 sits on the spawn point itself and has no outward direction.
  private static FP64 GetSpawnFacing(FPVector3 origin, FPVector3 position) {
    var away = position - origin;
    return away.sqrMagnitude == FP64.Zero ? FP64.Zero : FP64.Atan2(away.x, away.z);
  }
}
