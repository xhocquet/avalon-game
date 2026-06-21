using System.Collections.Generic;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.ECS.Systems;

namespace Meesles.Avalon {
  public static class SimulationSetup {
    private const int PlayerUnitTypeId = 1;
    public const int MinionUnitTypeId = 2;
    private const int BaseUnitTypeId = 100;
    private const int BaseHealth = 1000;
    private const int MapHalfExtent = 50;
    private const int BaseInset = 8;

    public static void RegisterSystems(EcsSimulation simulation) {
      simulation.AddSystem(new CommandSystem(), SystemPhase.Update);
      simulation.AddSystem(new WaveSpawnSystem(), SystemPhase.Update);

      simulation.AddSystem(new SpatialIndexSystem(), SystemPhase.Update);
      simulation.AddSystem(new TargetAcquisitionSystem(), SystemPhase.Update);
      simulation.AddSystem(new PathRequestSystem(), SystemPhase.Update);
      simulation.AddSystem(new PathfindingSystem(), SystemPhase.Update);
      simulation.AddSystem(new PathFollowSystem(), SystemPhase.Update);
      simulation.AddSystem(new LocalAvoidanceSystem(), SystemPhase.Update);
      simulation.AddSystem(new MovementIntentSystem(), SystemPhase.Update);
      simulation.AddSystem(new MinionMoveSystem(), SystemPhase.Update);

      simulation.AddSystem(new AttackIntentSystem(), SystemPhase.Update);
      simulation.AddSystem(new AttackCooldownSystem(), SystemPhase.Update);
      simulation.AddSystem(new DamageSystem(), SystemPhase.Update);
      simulation.AddSystem(new DeathSystem(), SystemPhase.Update);
      simulation.AddSystem(new RespawnSystem(), SystemPhase.Update);
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
      if (layout != null && layout.TryGetByTypeAndTeam(MapMarkerType.SpawnPoint, teamId, out var pos)) {
        return pos;
      }

      return GetTeamSpawnPosition(teamId);
    }

    private static void SpawnTeamBases(ref Frame frame, int maxPlayers, MapLayoutAsset layout) {
      for (int playerId = 1; playerId <= maxPlayers; playerId++) {
        int teamId = playerId;

        var baseEntity = frame.CreateEntity();
        FPVector3 basePosition =
          layout != null && layout.TryGetByTypeAndTeam(MapMarkerType.Base, teamId, out var layoutBasePos)
            ? layoutBasePos
            : GetTeamSpawnPosition(teamId);

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
        FPVector3 spawnPosition =
          layout != null && layout.TryGetByTypeAndTeam(MapMarkerType.SpawnPoint, teamId, out var layoutSpawnPos)
            ? layoutSpawnPos
            : GetTeamSpawnPosition(teamId);

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

    private static FPVector3 GetTeamSpawnPosition(int teamId) {
      FP64 corner = FP64.FromInt(MapHalfExtent - BaseInset);

      return teamId switch {
        1 => new FPVector3(-corner, FP64.Zero, -corner),
        2 => new FPVector3(corner, FP64.Zero, corner),
        3 => new FPVector3(corner, FP64.Zero, -corner),
        4 => new FPVector3(-corner, FP64.Zero, corner),
        _ => FPVector3.Zero,
      };
    }
  }
}
