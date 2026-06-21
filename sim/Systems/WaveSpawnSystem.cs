using System.Collections.Generic;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using Meesles.Avalon.Sim;
using Meesles.Avalon.Sim.Models;
using Meesles.Avalon.Sim.Assets;

namespace Meesles.Avalon {
  // Milestone M1: tick-driven minion waves. Spawn only — no movement or combat yet.
  // Every spawn point emits MinionsPerWave minions on a fixed cadence so we can watch
  // many networked entities stay in sync under the deterministic model.
  public class WaveSpawnSystem : ISystem {
    public void Update(ref Frame frame) {
      var rules = frame.AssetRegistry.Get<WaveRulesAsset>();
      if (rules == null) return;
      if (rules.SpawnIntervalTicks <= 0 || rules.MinionsPerWave <= 0) return;

      int rel = frame.Tick - rules.FirstWaveDelayTicks;
      if (rel < 0 || rel % rules.SpawnIntervalTicks != 0) return;
      int waveId = rel / rules.SpawnIntervalTicks;

      // Stop spawning once we hit the live-minion ceiling. Without death (M1/M2),
      // minions never leave, so this is what keeps us under MaxEntities.
      if (rules.MaxConcurrentMinions > 0 && CountMinions(ref frame) >= rules.MaxConcurrentMinions)
        return;

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
        SpawnWave(ref frame, rules, source.Position, source.TeamId, waveId);
    }

    private static int CountMinions(ref Frame frame) {
      int count = 0;
      var filter = frame.Filter<Minion>();
      while (filter.Next(out _)) count++;
      return count;
    }

    private static void SpawnWave(ref Frame frame, WaveRulesAsset rules, FPVector3 origin, int teamId, int waveId) {
      int count = rules.MinionsPerWave;

      // Center the cluster laterally around the spawn point: lateral = (i - (n-1)/2) * spacing.
      FP64 centerOffset = FP64.FromInt(count - 1) * rules.MinionSpacing * FP64.Half;

      for (int i = 0; i < count; i++) {
        FP64 lateral = FP64.FromInt(i) * rules.MinionSpacing - centerOffset;
        FPVector3 position = origin + new FPVector3(lateral, FP64.Zero, FP64.Zero);
        SpawnMinion(ref frame, rules, position, teamId, waveId);
      }
    }

    // Minions live as transform-only entities. Movement integrates the transform
    // directly and combat uses deterministic proximity queries; neither needs physics.
    private static void SpawnMinion(ref Frame frame, WaveRulesAsset rules, FPVector3 position, int teamId, int waveId) {
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
      frame.Add(entity, new Team { TeamId = teamId });
      frame.Add(entity, new Minion { WaveId = waveId });
      frame.Add(entity, new Health {
        Current = rules.MinionHealth,
        Max = rules.MinionHealth,
      });
      frame.Add(entity, new Combat {
        AttackDamage = rules.MinionAttackDamage,
        AttackRange = rules.MinionAttackRange,
        AttackCooldownTicks = rules.MinionAttackCooldownTicks,
        CooldownRemainingTicks = 0,
      });
    }
  }
}
