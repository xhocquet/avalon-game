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
    private const int CrystalUnitTypeId = 100;
    public const int TurretUnitTypeId = 101;
    private const int StructureHealth = 100;
    private const int TurretAttackDamage = 10;
    private const int TurretAttackCooldownTicks = 30;
    private static readonly FP64 TurretAttackRange = FP64.FromInt(12);

    // Faction ids live in the 200 range (== FactionAsset AssetId). DefaultFactionId is the
    // safety-net faction used when a player never sends a SelectFactionCommand within the grace
    // window (e.g. disconnect). Keep in sync with client/Sim/Data/Assets.json.
    public const int DefaultFactionId = 200;

    public static void RegisterSystems(EcsSimulation simulation, NavigationRuntime navigation = null) {
      simulation.AddSystem(new CommandSystem(moveNavAgentsDirectly: navigation == null), SystemPhase.Update);
      simulation.AddSystem(new HeroSpawnSystem(), SystemPhase.Update);
      simulation.AddSystem(new WaveSpawnSystem(), SystemPhase.Update);

      simulation.AddSystem(new TargetAcquisitionSystem(), SystemPhase.Update);
      simulation.AddSystem(new RespawnSystem(), SystemPhase.Update);
      if (navigation != null)
        simulation.AddSystem(new NavigationAgentSystem(navigation), SystemPhase.Update);

      simulation.AddSystem(new AttackIntentSystem(), SystemPhase.Update);
      simulation.AddSystem(new AttackCooldownSystem(), SystemPhase.Update);
      simulation.AddSystem(new DamageSystem(), SystemPhase.Update);
      simulation.AddSystem(new DeathSystem(), SystemPhase.Update);
      simulation.AddSystem(new ScoreSystem(), SystemPhase.LateUpdate);
      simulation.AddSystem(new EventSystem(), SystemPhase.LateUpdate);
    }

    public static void InitializeWorld(IKlothoEngine engine, int maxPlayers) {
      var frame = engine.PredictedFrame.Frame;
      InitializeWorld(ref frame, maxPlayers);
    }

    // spawnHeroesNow: when true, heroes are created immediately with the default faction (used by
    // the headless test/loadtest harness where picks are pre-resolved). When false (the networked
    // lobby flow), a PlayerFaction slot is seeded per player and HeroSpawnSystem spawns the hero
    // once the player's SelectFactionCommand lands, so the Faction is known at spawn time.
    public static void InitializeWorld(ref Frame frame, int maxPlayers, bool spawnHeroesNow = false) {
      UnitIdGenerator.Initialize(ref frame);
      var playerIds = GetPlayerIds(ref frame, maxPlayers);
      frame.AssetRegistry.TryGet<MapLayoutAsset>(out var layout);
      SpawnTeamCrystalsAndSpawnPoints(ref frame, playerIds.Count, layout);

      for (int playerIndex = 0; playerIndex < playerIds.Count; playerIndex++) {
        int playerId = playerIds[playerIndex];
        int teamId = playerIndex + 1;

        if (spawnHeroesNow) {
          // Immediate path (headless harness): spawn exactly as before so entity layout — and
          // therefore nav-agent indexing and state hashes — is identical to a heroes-at-init world.
          SpawnHero(ref frame, playerId, teamId, DefaultFactionId);
        }
        else {
          // Deferred path: seed a pick slot; HeroSpawnSystem spawns the hero once it's confirmed.
          var slot = frame.CreateEntity();
          frame.Add(slot, new PlayerFaction {
            PlayerId = playerId,
            TeamId = teamId,
            FactionId = DefaultFactionId,
            Confirmed = 0,
          });
        }
      }

      SpawnTeamTurrets(ref frame, playerIds.Count, layout);
    }

    // Spawns a single player's hero with its faction stamped on. Called by HeroSpawnSystem
    // (deferred until the faction pick is known); extracted from InitializeWorld so the world
    // setup and the deferred spawn share one definition of a hero.
    public static void SpawnHero(ref Frame frame, int playerId, int teamId, int factionId) {
      var playerStats = frame.AssetRegistry.Get<PlayerStatsAsset>();
      var combatStats = frame.AssetRegistry.Get<MinionStatsAsset>();

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
      frame.Add(entity, new Faction { FactionId = factionId });
      frame.Add(entity, new Hero {
        PlayerId = playerId,
        Level = 1,
        Experience = 0,
      });
      frame.Add(entity, new Unit {
        UnitId = UnitIdGenerator.Next(ref frame),
        UnitTypeId = PlayerUnitTypeId,
      });
      frame.Add(entity, new Controllable());

      if (playerStats != null) {
        frame.Add(entity, new Health {
          Current = playerStats.Health,
          Max = playerStats.Health,
        });
      }

      if (combatStats != null) {
        frame.Add(entity, new Combat {
          AttackDamage = combatStats.AttackDamage,
          AttackRange = combatStats.AttackRange,
          AttackCooldownTicks = combatStats.AttackCooldownTicks,
          CooldownRemainingTicks = 0,
        });
      }

      NavAgentSetup.AddNavAgent(ref frame, entity, initialPos, playerStats.MoveSpeed);
    }

    // Deterministic sorted list of active participant player ids. Index in this list + 1 is the
    // player's teamId — the same mapping used for crystals/turrets, so heroes line up with them.
    public static List<int> GetPlayerIds(ref Frame frame, int maxPlayers) {
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

    private static void SpawnTeamCrystalsAndSpawnPoints(ref Frame frame, int maxPlayers, MapLayoutAsset layout) {
      for (int playerId = 1; playerId <= maxPlayers; playerId++) {
        int teamId = playerId;

        var crystalEntity = frame.CreateEntity();
        FPVector3 crystalPosition = RequireMarkerPosition(layout, MapMarkerType.Crystal, teamId);

        frame.Add(crystalEntity, new TransformComponent {
          Position = crystalPosition,
          Rotation = FP64.Zero,
          Scale = FPVector3.One,
        });
        frame.Add(crystalEntity, new Unit {
          UnitId = UnitIdGenerator.Next(ref frame),
          UnitTypeId = CrystalUnitTypeId,
        });
        frame.Add(crystalEntity, new OwnerComponent { OwnerId = teamId });
        frame.Add(crystalEntity, new Team { TeamId = teamId });
        frame.Add(crystalEntity, new Crystal { CrystalId = teamId });
        frame.Add(crystalEntity, new Health {
          Current = StructureHealth,
          Max = StructureHealth,
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
          UnitTypeId = MinionUnitTypeId,
        });
      }
    }

    private static void SpawnTeamTurrets(ref Frame frame, int maxPlayers, MapLayoutAsset layout) {
      for (int teamId = 1; teamId <= maxPlayers; teamId++) {
        int turretIndex = 0;
        int typeInt = (int)MapMarkerType.Turret;
        int markerCount = layout?.MarkerTypes?.Length ?? 0;

        for (int i = 0; i < markerCount; i++) {
          if (layout.MarkerTypes[i] != typeInt || layout.MarkerTeams[i] != teamId)
            continue;

          turretIndex++;
          var turretEntity = frame.CreateEntity();
          frame.Add(turretEntity, new TransformComponent {
            Position = layout.MarkerPositions[i],
            Rotation = FP64.Zero,
            Scale = FPVector3.One,
          });
          frame.Add(turretEntity, new Unit {
            UnitId = UnitIdGenerator.Next(ref frame),
            UnitTypeId = TurretUnitTypeId,
          });
          frame.Add(turretEntity, new Team { TeamId = teamId });
          frame.Add(turretEntity, new Turret { TurretId = teamId * 100 + turretIndex });
          frame.Add(turretEntity, new Health {
            Current = StructureHealth,
            Max = StructureHealth,
          });
          frame.Add(turretEntity, new Combat {
            AttackDamage = TurretAttackDamage,
            AttackRange = TurretAttackRange,
            AttackCooldownTicks = TurretAttackCooldownTicks,
            CooldownRemainingTicks = 0,
          });
        }
      }
    }

    private static FPVector3 RequireMarkerPosition(MapLayoutAsset layout, MapMarkerType type, int teamId) {
      if (layout != null && layout.TryGetByTypeAndTeam(type, teamId, out var position))
        return position;

      throw new System.InvalidOperationException($"MapLayoutAsset is missing {type} marker for team {teamId}.");
    }
  }
}
