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
    private const int HeroSpawnInset = 8;

    public static void RegisterSystems(EcsSimulation simulation) {
      simulation.AddSystem(new MovementSystem(), SystemPhase.Update);
      simulation.AddSystem(new WaveSpawnSystem(), SystemPhase.Update);
      simulation.AddSystem(new MinionMoveSystem(), SystemPhase.Update);
      simulation.AddSystem(new RespawnSystem(), SystemPhase.Update);
      simulation.AddSystem(new ScoreSystem(), SystemPhase.LateUpdate);
      simulation.AddSystem(new EventSystem(), SystemPhase.LateUpdate);
    }

    public static void InitializeWorld(IKlothoEngine engine, int maxPlayers) {
      var frame = engine.PredictedFrame.Frame;
      InitializeWorld(ref frame, maxPlayers);
    }

    public static void InitializeWorld(ref Frame frame, int maxPlayers) {
      UnitIdGenerator.Initialize(ref frame);
      SpawnTeamBases(ref frame, maxPlayers);

      for (int playerId = 0; playerId < maxPlayers; playerId++) {
        int teamId = TeamAssignment.GetTeamIdForPlayer(playerId + 1);
        var entity = frame.CreateEntity();
        FPVector3 initialPos = GetHeroSpawnPositionForTeam(ref frame, teamId);

        frame.Add(entity, new TransformComponent {
          Position = initialPos,
          Rotation = FP64.Zero,
          Scale = FPVector3.One,
        });
        frame.Add(entity, new OwnerComponent { OwnerId = playerId + 1 });
        frame.Add(entity, new PlayerComponent { PlayerId = playerId + 1 });
        frame.Add(entity, new Team { TeamId = teamId });
        frame.Add(entity, new Hero {
          PlayerId = playerId + 1,
          Level = 1,
          Experience = 0,
        });
        frame.Add(entity, new Unit {
          UnitId = UnitIdGenerator.Next(ref frame),
          UnitTypeId = PlayerUnitTypeId,
        });
      }
    }

    public static FPVector3 GetHeroSpawnPositionForTeam(ref Frame frame, int teamId) {
      if (!TryGetSpawnPointPosition(ref frame, teamId, out var spawnPosition))
        spawnPosition = GetTeamSpawnPosition(teamId);

      return spawnPosition + GetHeroSpawnOffset(teamId);
    }

    private static void SpawnTeamBases(ref Frame frame, int maxPlayers) {
      for (int playerId = 1; playerId <= maxPlayers; playerId++) {
        int teamId = TeamAssignment.GetTeamIdForPlayer(playerId);
        var entity = frame.CreateEntity();
        FPVector3 position = GetTeamSpawnPosition(teamId);

        frame.Add(entity, new TransformComponent {
          Position = position,
          Rotation = FP64.Zero,
          Scale = FPVector3.One,
        });
        frame.Add(entity, new Unit {
          UnitId = UnitIdGenerator.Next(ref frame),
          UnitTypeId = BaseUnitTypeId,
        });
        frame.Add(entity, new Team { TeamId = teamId });
        frame.Add(entity, new Base { BaseId = teamId });
        frame.Add(entity, new SpawnPoint {
          SpawnPointId = teamId,
          TeamId = teamId,
          LaneId = 0,
          UnitTypeId = PlayerUnitTypeId,
        });
        frame.Add(entity, new Health {
          Current = BaseHealth,
          Max = BaseHealth,
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

    private static bool TryGetSpawnPointPosition(ref Frame frame, int teamId, out FPVector3 position) {
      var filter = frame.Filter<SpawnPoint, TransformComponent>();
      while (filter.Next(out var entity)) {
        ref readonly var spawnPoint = ref frame.Get<SpawnPoint>(entity);
        if (spawnPoint.TeamId != teamId) continue;

        ref readonly var transform = ref frame.Get<TransformComponent>(entity);
        position = transform.Position;
        return true;
      }

      position = FPVector3.Zero;
      return false;
    }

    private static FPVector3 GetHeroSpawnOffset(int teamId) {
      FP64 inset = FP64.FromInt(HeroSpawnInset);

      return teamId switch {
        1 => new FPVector3(inset, FP64.Zero, inset),
        2 => new FPVector3(-inset, FP64.Zero, -inset),
        3 => new FPVector3(-inset, FP64.Zero, inset),
        4 => new FPVector3(inset, FP64.Zero, -inset),
        _ => FPVector3.Zero,
      };
    }
  }
}
