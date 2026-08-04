using System.Collections.Generic;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using Meesles.Avalon.Sim.Factories;
using Meesles.Avalon.Sim.Navigation;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon;

public class WaveSpawnSystem : ISystem {
  private readonly List<EntityRef> _nearbyMinions = new();
  private readonly List<(FPVector3 Position, int TeamId)> _sources = new();

  // Built on first use: the cell size derives from MinionSpacing, which isn't available at
  // construction time. Rebuilt if the asset value changes.
  private SpatialHashGrid _occupancyGrid;
  private FP64 _occupancyGridCellSize;

  public void Update(ref Frame frame) {
    var rules = frame.AssetRegistry.Get<WaveRulesAsset>();
    var stats = frame.AssetRegistry.Get<MinionStatsAsset>();
    if (rules.SpawnIntervalTicks <= 0 || rules.MinionsPerWave <= 0 || rules.MinionSpacing <= FP64.Zero) return;

    var rel = frame.Tick - rules.FirstWaveDelayTicks;
    if (rel < 0 || rel % rules.SpawnIntervalTicks != 0) return;
    var waveId = rel / rules.SpawnIntervalTicks;

    BuildOccupancyGrid(ref frame, rules.MinionSpacing);

    // Snapshot spawn points before creating entities so we don't mutate the set
    // we're iterating. Filter order is deterministic, so this stays in sync.
    _sources.Clear();
    var filter = frame.Filter<SpawnPoint, TeamComponent, TransformComponent>();
    while (filter.Next(out var entity)) {
      ref readonly var team = ref frame.Get<TeamComponent>(entity);
      ref readonly var transform = ref frame.Get<TransformComponent>(entity);
      _sources.Add((transform.Position, team.TeamId));
    }

    foreach (var source in _sources)
      SpawnWave(ref frame, rules, stats, source.Position, source.TeamId, waveId);
  }

  // Broad-phase: bucket every minion once per spawn tick so the slot search only distance-checks
  // the handful sitting near each slot instead of every minion on the map. Spawned minions are
  // inserted as they're created so later slots in the same wave see them.
  private void BuildOccupancyGrid(ref Frame frame, FP64 spacing) {
    if (_occupancyGrid == null || _occupancyGridCellSize != spacing) {
      _occupancyGrid = new SpatialHashGrid(spacing);
      _occupancyGridCellSize = spacing;
    }

    _occupancyGrid.Clear();

    var filter = frame.Filter<Minion, TeamComponent, TransformComponent>();
    while (filter.Next(out var entity)) {
      ref readonly var transform = ref frame.GetReadOnly<TransformComponent>(entity);
      _occupancyGrid.Insert(entity, transform.Position.ToXZ());
    }
  }

  private void SpawnWave(ref Frame frame, WaveRulesAsset rules, MinionStatsAsset stats, FPVector3 origin,
    int teamId, int waveId) {
    var count = rules.MinionsPerWave;

    // Slots below the last free one were occupied and stay occupied — nothing is removed mid-wave —
    // so each minion resumes the scan past the slot the previous one took instead of restarting.
    var searchStart = 0;

    for (var i = 0; i < count; i++) {
      var slotIndex = GetFirstFreeSlot(ref frame, origin, teamId, rules.MinionSpacing, searchStart);
      var position = GetSpawnPosition(origin, rules.MinionSpacing, slotIndex);
      var minion = MinionFactory.Spawn(ref frame, stats, position, GetSpawnFacing(origin, position), teamId, waveId);
      _occupancyGrid.Insert(minion, position.ToXZ());
      searchStart = slotIndex + 1;
    }
  }

  private int GetFirstFreeSlot(ref Frame frame, FPVector3 origin, int teamId, FP64 spacing, int startSlot) {
    var slot = startSlot;
    while (IsSlotOccupied(ref frame, origin, teamId, spacing, slot))
      slot++;

    return slot;
  }

  private bool IsSlotOccupied(ref Frame frame, FPVector3 origin, int teamId, FP64 spacing, int slot) {
    var slotPosition = GetSpawnPosition(origin, spacing, slot);
    var occupiedRadius = spacing * FP64.Half;
    var occupiedRadiusSqr = occupiedRadius * occupiedRadius;

    // The grid filters on XZ distance, which never exceeds the full 3D distance, so its results are
    // a superset of the occupants and the exact 3D test below still decides.
    _occupancyGrid.QueryRadius(slotPosition.ToXZ(), occupiedRadius, _nearbyMinions);

    for (var i = 0; i < _nearbyMinions.Count; i++) {
      var entity = _nearbyMinions[i];
      ref readonly var team = ref frame.GetReadOnly<TeamComponent>(entity);
      if (team.TeamId != teamId)
        continue;

      ref readonly var transform = ref frame.GetReadOnly<TransformComponent>(entity);
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
