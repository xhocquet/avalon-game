using System.Collections.Generic;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using Meesles.Avalon.Sim;
using Meesles.Avalon.Sim.Models;
using Meesles.Avalon.Sim.Assets;

namespace Meesles.Avalon {
  public class WaveSpawnSystem : ISystem {
    public void Update(ref Frame frame) {
      var rules = frame.AssetRegistry.Get<WaveRulesAsset>();
      var stats = frame.AssetRegistry.Get<MinionStatsAsset>();
      if (rules == null || stats == null) return;
      if (rules.SpawnIntervalTicks <= 0 || rules.MinionsPerWave <= 0) return;

      int rel = frame.Tick - rules.FirstWaveDelayTicks;
      if (rel < 0 || rel % rules.SpawnIntervalTicks != 0) return;
      int waveId = rel / rules.SpawnIntervalTicks;

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

    private static void SpawnWave(ref Frame frame, WaveRulesAsset rules, MinionStatsAsset stats, FPVector3 origin, int teamId, int waveId) {
      int count = rules.MinionsPerWave;

      // Center the cluster laterally around the spawn point: lateral = (i - (n-1)/2) * spacing.
      FP64 centerOffset = FP64.FromInt(count - 1) * rules.MinionSpacing * FP64.Half;

      for (int i = 0; i < count; i++) {
        FP64 lateral = FP64.FromInt(i) * rules.MinionSpacing - centerOffset;
        FPVector3 position = origin + new FPVector3(lateral, FP64.Zero, FP64.Zero);
        SpawnMinion(ref frame, stats, position, teamId, waveId);
      }
    }

    // Minions live as transform-only entities. Movement integrates the transform
    // directly and combat uses deterministic proximity queries; neither needs physics.
    private static void SpawnMinion(ref Frame frame, MinionStatsAsset stats, FPVector3 position, int teamId, int waveId) {
      var entity = frame.CreateEntity();

      frame.Add(entity, new TransformComponent {
        Position = position,
        Rotation = FP64.Zero,
        Scale = FPVector3.One,
      });
      frame.Add(entity, new Unit {
        UnitId = UnitIdGenerator.Next(ref frame),
        UnitTypeId = SimulationSetup.MinionUnitTypeId,
      });
      frame.Add(entity, new OwnerComponent { OwnerId = teamId });
      frame.Add(entity, new Team { TeamId = teamId });
      frame.Add(entity, new Minion { WaveId = waveId });
      frame.Add(entity, new Health {
        Current = stats.Health,
        Max = stats.Health,
      });
      frame.Add(entity, new Combat {
        AttackDamage = stats.AttackDamage,
        AttackRange = stats.AttackRange,
        AttackCooldownTicks = stats.AttackCooldownTicks,
        CooldownRemainingTicks = 0,
      });
    }
  }
}
