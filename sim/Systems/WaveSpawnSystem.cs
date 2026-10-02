using System.Collections.Generic;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using Meesles.Avalon.Sim.Factories;
using Meesles.Avalon.Sim.Navigation;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim;

public class WaveSpawnSystem : ISystem {
  // Bounds spawn-slot search and footprint.
  internal const int MaxRing = 8;

  private readonly List<EntityRef> _nearbyMinions = new();
  private readonly List<(FPVector3 Position, int TeamId)> _sources = new();

  // Rebuilt when authored minion spacing changes.
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

    // Snapshot spawn points before creating minions.
    _sources.Clear();
    var filter = frame.Filter<SpawnPoint, Team, TransformComponent>();
    while (filter.Next(out var entity)) {
      ref readonly var team = ref frame.GetReadOnly<Team>(entity);
      ref readonly var transform = ref frame.GetReadOnly<TransformComponent>(entity);
      _sources.Add((transform.Position, team.TeamId));
    }

    foreach (var source in _sources)
      SpawnWave(ref frame, rules, stats, source.Position, source.TeamId, waveId);
  }

  // Insert spawned minions so later slots see them.
  private void BuildOccupancyGrid(ref Frame frame, FP64 spacing) {
    if (_occupancyGrid == null || _occupancyGridCellSize != spacing) {
      _occupancyGrid = new SpatialHashGrid(spacing);
      _occupancyGridCellSize = spacing;
    }

    _occupancyGrid.Clear();

    var filter = frame.Filter<Minion, Team, TransformComponent>();
    while (filter.Next(out var entity)) {
      ref readonly var transform = ref frame.GetReadOnly<TransformComponent>(entity);
      _occupancyGrid.Insert(entity, transform.Position.ToXZ());
    }
  }

  private void SpawnWave(ref Frame frame, WaveRulesAsset rules, MinionStatsAsset stats, FPVector3 origin,
    int teamId, int waveId) {
    var count = rules.MinionsPerWave;

    // Reuse one cursor because occupancy only grows during the wave.
    var cursor = default(SlotCursor);

    for (var i = 0; i < count; i++) {
      SeekFreeSlot(ref frame, ref cursor, origin, teamId, rules.MinionSpacing);
      var position = cursor.GetPosition(origin, rules.MinionSpacing);
      var minion = MinionFactory.Spawn(ref frame, stats, position, GetSpawnFacing(origin, position), teamId, waveId);
      _occupancyGrid.Insert(minion, position.ToXZ());
      cursor.Advance();
    }
  }

  // Fills outward, stacking deterministically at the final slot when full.
  private void SeekFreeSlot(ref Frame frame, ref SlotCursor cursor, FPVector3 origin, int teamId, FP64 spacing) {
    while (!cursor.AtLastSlot && IsSlotOccupied(ref frame, cursor.GetPosition(origin, spacing), teamId, spacing))
      cursor.Advance();
  }

  private bool IsSlotOccupied(ref Frame frame, FPVector3 slotPosition, int teamId, FP64 spacing) {
    var occupiedRadius = spacing * FP64.Half;
    var occupiedRadiusSqr = occupiedRadius * occupiedRadius;

    // XZ query results are a superset; the 3D check decides occupancy.
    _occupancyGrid.QueryRadius(slotPosition.ToXZ(), occupiedRadius, _nearbyMinions);

    for (var i = 0; i < _nearbyMinions.Count; i++) {
      var entity = _nearbyMinions[i];
      ref readonly var team = ref frame.GetReadOnly<Team>(entity);
      if (team.TeamId != teamId)
        continue;

      ref readonly var transform = ref frame.GetReadOnly<TransformComponent>(entity);
      if ((transform.Position - slotPosition).sqrMagnitude <= occupiedRadiusSqr)
        return true;
    }

    return false;
  }

  // Hex-packed rings centered on the spawn point, indexed for O(1) outward traversal.
  internal struct SlotCursor {
    private int _ring;
    private int _slot;

    public readonly bool AtLastSlot => _ring >= MaxRing && _slot >= 6 * MaxRing - 1;

    public void Advance() {
      if (AtLastSlot)
        return;

      if (_ring == 0) {
        _ring = 1;
        return;
      }

      _slot++;
      if (_slot < 6 * _ring)
        return;

      _ring++;
      _slot = 0;
    }

    public readonly FPVector3 GetPosition(FPVector3 origin, FP64 spacing) {
      if (_ring == 0)
        return origin;

      var radius = spacing * FP64.FromInt(_ring);
      var angle = FP64.TwoPi / FP64.FromInt(6 * _ring) * FP64.FromInt(_slot);
      var offset = new FPVector3(FP64.Sin(angle), FP64.Zero, FP64.Cos(angle)) * radius;
      return origin + offset;
    }
  }

  // Face outward; the center slot has no direction.
  private static FP64 GetSpawnFacing(FPVector3 origin, FPVector3 position) {
    var away = position - origin;
    return away.sqrMagnitude == FP64.Zero ? FP64.Zero : FP64.Atan2(away.x, away.z);
  }
}
