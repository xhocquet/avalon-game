using System.Collections.Generic;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Models;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.ECS.Systems;

namespace Meesles.Avalon.Sim {
  public static class SimulationSetup {
    private const int PlayerUnitTypeId = 1;
    public const int MinionUnitTypeId = 2;
    private const int BaseUnitTypeId = 100;
    private const int BaseHealth = 1000;

    public static void RegisterSystems(EcsSimulation simulation, NavigationRuntime navigation = null) {
      simulation.AddSystem(new CommandSystem(moveNavAgentsDirectly: navigation == null), SystemPhase.Update);
      simulation.AddSystem(new WaveSpawnSystem(), SystemPhase.Update);

      simulation.AddSystem(new SpatialIndexSystem(), SystemPhase.Update);
      simulation.AddSystem(new TargetAcquisitionSystem(), SystemPhase.Update);
      simulation.AddSystem(new PathRequestSystem(), SystemPhase.Update);
      simulation.AddSystem(new PathfindingSystem(), SystemPhase.Update);
      simulation.AddSystem(new PathFollowSystem(), SystemPhase.Update);
      simulation.AddSystem(new RespawnSystem(), SystemPhase.Update);
      if (navigation != null)
        simulation.AddSystem(new NavigationAgentSystem(navigation), SystemPhase.Update);
      else
        simulation.AddSystem(new LocalAvoidanceSystem(), SystemPhase.Update);
      simulation.AddSystem(new MovementIntentSystem(), SystemPhase.Update);

      simulation.AddSystem(new AttackIntentSystem(), SystemPhase.Update);
      simulation.AddSystem(new AttackCooldownSystem(), SystemPhase.Update);
      simulation.AddSystem(new DamageSystem(), SystemPhase.Update);
      simulation.AddSystem(new DeathSystem(), SystemPhase.Update);
      simulation.AddSystem(new RewardSystem(), SystemPhase.LateUpdate);
      simulation.AddSystem(new ScoreSystem(), SystemPhase.LateUpdate);
      simulation.AddSystem(new EventSystem(), SystemPhase.LateUpdate);
    }

    public static void InitializeWorld(IKlothoEngine engine, int maxPlayers) {
      var frame = engine.PredictedFrame.Frame;
      InitializeWorld(ref frame, maxPlayers);
    }

    public static void InitializeWorld(ref Frame frame, int maxPlayers) {
      UnitIdGenerator.Initialize(ref frame);
      var playerIds = GetPlayerIds(ref frame, maxPlayers);
      var playerStats = frame.AssetRegistry.Get<PlayerStatsAsset>();
      var combatStats = frame.AssetRegistry.Get<MinionStatsAsset>();
      frame.AssetRegistry.TryGet<MapLayoutAsset>(out var layout);
      SpawnTeamBases(ref frame, playerIds.Count, layout);

      for (int playerIndex = 0; playerIndex < playerIds.Count; playerIndex++) {
        int playerId = playerIds[playerIndex];
        int teamId = playerIndex + 1;
        var entity = frame.CreateEntity();
        FPVector3 initialPos = GetHeroSpawnPositionForTeam(ref frame, teamId);

        frame.Add(entity, new TransformComponent {
          Position = initialPos,
          Rotation = FP64.Zero,
          Scale = FPVector3.One,
        });
        frame.Add(entity, new OwnerComponent { OwnerId = playerId });
        frame.Add(entity, new Player { PlayerId = playerId });
        frame.Add(entity, new Team { TeamId = teamId });
        frame.Add(entity, new Hero {
          PlayerId = playerId,
          Level = 1,
          Experience = 0,
        });
        frame.Add(entity, new Unit {
          UnitId = UnitIdGenerator.Next(ref frame),
          UnitTypeId = PlayerUnitTypeId,
        });
        if (playerStats != null) {
          frame.Add(entity, new Health {
            Current = playerStats.Health,
            Max = playerStats.Health,
          });
          NavAgentSetup.AddNavAgent(ref frame, entity, initialPos, playerStats.MoveSpeed);
        }
        if (combatStats != null) {
          frame.Add(entity, new Combat {
            AttackDamage = combatStats.AttackDamage,
            AttackRange = combatStats.AttackRange,
            AttackCooldownTicks = combatStats.AttackCooldownTicks,
            CooldownRemainingTicks = 0,
          });
        }
      }
    }

    private static List<int> GetPlayerIds(ref Frame frame, int maxPlayers) {
      var playerIds = new List<int>();
      var filter = frame.Filter<SessionParticipantComponent>();
      while (filter.Next(out var entity)) {
        ref readonly var participant = ref frame.GetReadOnly<SessionParticipantComponent>(entity);
        playerIds.Add(participant.PlayerId);
      }

      if (playerIds.Count == 0) {
        for (int playerId = 1; playerId <= maxPlayers; playerId++)
          playerIds.Add(playerId);
      }

      playerIds.Sort();
      return playerIds;
    }

    public static FPVector3 GetHeroSpawnPositionForTeam(ref Frame frame, int teamId) {
      frame.AssetRegistry.TryGet<MapLayoutAsset>(out var layout);
      return RequireMarkerPosition(layout, MapMarkerType.SpawnPoint, teamId);
    }

    private static void SpawnTeamBases(ref Frame frame, int maxPlayers, MapLayoutAsset layout) {
      for (int playerId = 1; playerId <= maxPlayers; playerId++) {
        int teamId = playerId;

        var baseEntity = frame.CreateEntity();
        FPVector3 basePosition = RequireMarkerPosition(layout, MapMarkerType.Base, teamId);

        frame.Add(baseEntity, new TransformComponent {
          Position = basePosition,
          Rotation = FP64.Zero,
          Scale = FPVector3.One,
        });
        frame.Add(baseEntity, new Unit {
          UnitId = UnitIdGenerator.Next(ref frame),
          UnitTypeId = BaseUnitTypeId,
        });
        frame.Add(baseEntity, new Team { TeamId = teamId });
        frame.Add(baseEntity, new Base { BaseId = teamId });
        frame.Add(baseEntity, new Health {
          Current = BaseHealth,
          Max = BaseHealth,
        });

        var spawnEntity = frame.CreateEntity();
        FPVector3 spawnPosition = RequireMarkerPosition(layout, MapMarkerType.SpawnPoint, teamId);

        frame.Add(spawnEntity, new TransformComponent {
          Position = spawnPosition,
          Rotation = FP64.Zero,
          Scale = FPVector3.One,
        });
        frame.Add(spawnEntity, new Team { TeamId = teamId });
        frame.Add(spawnEntity, new SpawnPoint {
          SpawnPointId = teamId,
          TeamId = teamId,
          UnitTypeId = MinionUnitTypeId,
        });
      }
    }

    private static FPVector3 RequireMarkerPosition(MapLayoutAsset layout, MapMarkerType type, int teamId) {
      if (layout != null && layout.TryGetByTypeAndTeam(type, teamId, out var position))
        return position;

      throw new System.InvalidOperationException($"MapLayoutAsset is missing {type} marker for team {teamId}.");
    }
  }
}
