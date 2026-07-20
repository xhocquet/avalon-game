using System.Collections.Generic;
using Meesles.Avalon.Sim;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Models;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon;

public class WaveSpawnSystem : ISystem {
  public void Update(ref Frame frame) {
    var rules = frame.AssetRegistry.Get<WaveRulesAsset>();
    var stats = frame.AssetRegistry.Get<MinionStatsAsset>();
    if (rules == null || stats == null) return;
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
      SpawnMinion(ref frame, stats, position, teamId, waveId);
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

  private static FPVector3 GetSpawnPosition(FPVector3 origin, FP64 spacing, int index) {
    var forward = new FPVector3(-origin.x, FP64.Zero, -origin.z);
    if (forward.sqrMagnitude == FP64.Zero)
      forward = new FPVector3(FP64.Zero, FP64.Zero, FP64.One);
    else
      forward = forward.normalized;

    var ring = 0;
    var ringStart = 0;
    var ringCapacity = GetRingCapacity(ring);
    while (index >= ringStart + ringCapacity) {
      ringStart += ringCapacity;
      ring++;
      ringCapacity = GetRingCapacity(ring);
    }

    var slot = index - ringStart;
    var radius = spacing * FP64.FromInt(ring + 2);
    var angle = GetArcAngle(slot, ringCapacity);
    var offset = RotateXZ(forward, angle) * radius;
    return origin + offset;
  }

  private static int GetRingCapacity(int ring) {
    return 4 + ring;
  }

  private static FP64 GetArcAngle(int slot, int ringCount) {
    if (ringCount <= 1)
      return FP64.Zero;

    var arc = FP64.HalfPi;
    var step = arc / FP64.FromInt(ringCount - 1);
    return step * FP64.FromInt(slot) - arc * FP64.Half;
  }

  private static FPVector3 RotateXZ(FPVector3 vector, FP64 angle) {
    var sin = FP64.Sin(angle);
    var cos = FP64.Cos(angle);
    return new FPVector3(
      vector.x * cos + vector.z * sin,
      FP64.Zero,
      -vector.x * sin + vector.z * cos);
  }

  private static void SpawnMinion(ref Frame frame, MinionStatsAsset stats, FPVector3 position, int teamId,
    int waveId) {
    var entity = frame.CreateEntity();

    frame.Add(entity, new TransformComponent {
      Position = position,
      Rotation = FP64.Zero,
      Scale = FPVector3.One
    });
    frame.Add(entity, new Unit {
      UnitId = UnitIdGenerator.Next(ref frame),
      UnitTypeId = SimulationSetup.MinionUnitTypeId
    });
    frame.Add(entity, new OwnerComponent { OwnerId = teamId });
    frame.Add(entity, new Team { TeamId = teamId });
    frame.Add(entity, new Minion { WaveId = waveId });
    frame.Add(entity, new Controllable());
    frame.Add(entity, new Health {
      Current = stats.Health,
      Max = stats.Health
    });
    frame.Add(entity, new Combat {
      AttackDamage = stats.AttackDamage,
      AttackRange = stats.AttackRange,
      AttackCooldownTicks = stats.AttackCooldownTicks,
      CooldownRemainingTicks = 0
    });

    NavAgentSetup.AddNavAgent(ref frame, entity, position, stats.MoveSpeed, NavAgentSetup.MinionRadius);
  }
}
