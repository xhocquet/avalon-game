using System;
using System.Collections.Generic;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Models;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.ECS.Systems;

namespace Meesles.Avalon.Sim;

public static class SimulationSetup {
  private const int PlayerUnitTypeId = 1;
  public const int MinionUnitTypeId = 2;
  private const int CrystalUnitTypeId = 100;
  public const int TurretUnitTypeId = 101;
  private const int StructureHealth = 100;
  private const int TurretAttackDamage = 10;
  private const int TurretAttackCooldownTicks = 30;

  // Faction ids live in the 200 range (== FactionAsset AssetId). DefaultFactionId is the
  // safety-net faction used when a player never sends a SelectFactionCommand within the grace
  // window (e.g. disconnect). Keep in sync with client/Sim/Data/Assets.json.
  public const int DefaultFactionId = 200;

  // Ticks of setup grace before the match is considered started. HeroSpawnSystem falls back to the
  // default faction after this, and TeamPruneSystem culls the bases of teams no player is on at the
  // same boundary. Keep this >= WaveRulesAsset.FirstWaveDelayTicks and register TeamPruneSystem
  // before WaveSpawnSystem so teamless spawn points are gone before the first wave spawns.
  public const int SetupGraceTicks = 30;
  private static readonly FP64 TurretAttackRange = FP64.FromInt(12);

  public static void RegisterSystems(EcsSimulation simulation, NavigationRuntime navigation = null) {
    simulation.AddSystem(new CommandSystem(navigation), SystemPhase.Update);
    simulation.AddSystem(new HeroSpawnSystem(), SystemPhase.Update);
    simulation.AddSystem(new TeamPruneSystem(), SystemPhase.Update);
    simulation.AddSystem(new WaveSpawnSystem(), SystemPhase.Update);
    simulation.AddSystem(new InventorySystem(), SystemPhase.Update);
    simulation.AddSystem(new StatsSystem(), SystemPhase.Update);
    simulation.AddSystem(new OasisSpawnSystem(), SystemPhase.Update);
    simulation.AddSystem(new PickupSystem(), SystemPhase.Update);
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

  // spawnHeroesNow: use default factions or selected ones
  public static void InitializeWorld(ref Frame frame, int maxPlayers, bool spawnHeroesNow = false) {
    UnitIdGenerator.Initialize(ref frame);
    var playerIds = GetPlayerIds(ref frame, maxPlayers);
    frame.AssetRegistry.TryGet<MapLayoutAsset>(out var layout);

    // The map authors a base per team (teams 1..K). In the deferred lobby flow we spawn every
    // authored team's structures so the whole map is populated during setup, then TeamPruneSystem
    // culls the teams no player ended up on once picks settle. The headless spawn-heroes-now path
    // already knows the final roster, so it spawns only the rostered teams — no phantom bases, and
    // stable unit-id numbering for the harness.
    var structureTeams = spawnHeroesNow ? BuildRosterTeamIds(playerIds.Count) : GetAuthoredTeamIds(layout);
    SpawnTeamCrystalsAndSpawnPoints(ref frame, structureTeams, layout);

    for (var playerIndex = 0; playerIndex < playerIds.Count; playerIndex++) {
      var playerId = playerIds[playerIndex];
      var teamId = playerIndex + 1;

      if (spawnHeroesNow) {
        SpawnHero(ref frame, playerId, teamId, DefaultFactionId);
      }
      else {
        var slot = frame.CreateEntity();
        frame.Add(slot, new PlayerFaction {
          PlayerId = playerId,
          TeamId = teamId,
          FactionId = DefaultFactionId,
          Confirmed = 0
        });
      }
    }

    SpawnTeamTurrets(ref frame, structureTeams, layout);
    SpawnOases(ref frame, layout);
    SpawnPickups(ref frame, layout);
  }

  // Team ids 1..count — the deterministic roster mapping (teamId == playerIndex + 1) used when the
  // final set of players is already known.
  private static List<int> BuildRosterTeamIds(int count) {
    var teams = new List<int>(count);
    for (var teamId = 1; teamId <= count; teamId++)
      teams.Add(teamId);

    return teams;
  }

  // Distinct team ids the map authors a base (Crystal marker) for, sorted ascending for determinism.
  private static List<int> GetAuthoredTeamIds(MapLayoutAsset layout) {
    var teams = new List<int>();
    var markerCount = layout?.MarkerTypes?.Length ?? 0;
    var crystalType = (int)MapMarkerType.Crystal;

    for (var i = 0; i < markerCount; i++) {
      if (layout.MarkerTypes[i] != crystalType)
        continue;

      var teamId = layout.MarkerTeams[i];
      if (teamId > 0 && !teams.Contains(teamId))
        teams.Add(teamId);
    }

    teams.Sort();
    return teams;
  }

  public static void SpawnHero(ref Frame frame, int playerId, int teamId, int factionId) {
    var playerStats = frame.AssetRegistry.Get<PlayerStatsAsset>();
    var combatStats = frame.AssetRegistry.Get<MinionStatsAsset>();

    var entity = frame.CreateEntity();
    var initialPos = GetHeroSpawnPositionForTeam(ref frame, teamId);

    frame.Add(entity, new TransformComponent {
      Position = initialPos,
      Rotation = FP64.Zero,
      Scale = FPVector3.One
    });
    frame.Add(entity, new OwnerComponent { OwnerId = playerId });
    frame.Add(entity, new Player { PlayerId = playerId });
    frame.Add(entity, new Team { TeamId = teamId });
    frame.Add(entity, new Faction { FactionId = factionId });
    frame.Add(entity, new Hero {
      PlayerId = playerId,
      Level = 1,
      Experience = 0
    });
    frame.Add(entity, new Unit {
      UnitId = UnitIdGenerator.Next(ref frame),
      UnitTypeId = PlayerUnitTypeId
    });
    frame.Add(entity, new Controllable());
    frame.Add(entity, new Inventory());
    frame.Add(entity, new Stats { Strength = 10 });

    if (playerStats != null)
      frame.Add(entity, new Health {
        Current = playerStats.Health,
        Max = playerStats.Health
      });

    if (combatStats != null)
      frame.Add(entity, new Combat {
        AttackDamage = combatStats.AttackDamage,
        AttackRange = combatStats.AttackRange,
        AttackCooldownTicks = combatStats.AttackCooldownTicks,
        CooldownRemainingTicks = 0
      });

    NavAgentSetup.AddNavAgent(ref frame, entity, initialPos, playerStats.MoveSpeed, NavAgentSetup.HeroRadius);
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

    if (playerIds.Count == 0)
      for (var playerId = 1; playerId <= maxPlayers; playerId++)
        playerIds.Add(playerId);

    playerIds.Sort();
    return playerIds;
  }

  public static FPVector3 GetHeroSpawnPositionForTeam(ref Frame frame, int teamId) {
    frame.AssetRegistry.TryGet<MapLayoutAsset>(out var layout);
    return RequireMarkerPosition(layout, MapMarkerType.SpawnPoint, teamId);
  }

  private static void SpawnTeamCrystalsAndSpawnPoints(ref Frame frame, List<int> teamIds, MapLayoutAsset layout) {
    foreach (var teamId in teamIds) {
      var crystalEntity = frame.CreateEntity();
      var crystalPosition = RequireMarkerPosition(layout, MapMarkerType.Crystal, teamId);

      frame.Add(crystalEntity, new TransformComponent {
        Position = crystalPosition,
        Rotation = FP64.Zero,
        Scale = FPVector3.One
      });
      frame.Add(crystalEntity, new Unit {
        UnitId = UnitIdGenerator.Next(ref frame),
        UnitTypeId = CrystalUnitTypeId
      });
      frame.Add(crystalEntity, new OwnerComponent { OwnerId = teamId });
      frame.Add(crystalEntity, new Team { TeamId = teamId });
      frame.Add(crystalEntity, new Crystal { CrystalId = teamId });
      frame.Add(crystalEntity, new Health {
        Current = StructureHealth,
        Max = StructureHealth
      });

      var spawnEntity = frame.CreateEntity();
      var spawnPosition = RequireMarkerPosition(layout, MapMarkerType.SpawnPoint, teamId);

      frame.Add(spawnEntity, new TransformComponent {
        Position = spawnPosition,
        Rotation = FP64.Zero,
        Scale = FPVector3.One
      });
      frame.Add(spawnEntity, new Team { TeamId = teamId });
      frame.Add(spawnEntity, new SpawnPoint {
        SpawnPointId = teamId,
        UnitTypeId = MinionUnitTypeId
      });
    }
  }

  private static void SpawnTeamTurrets(ref Frame frame, List<int> teamIds, MapLayoutAsset layout) {
    foreach (var teamId in teamIds) {
      var turretIndex = 0;
      var typeInt = (int)MapMarkerType.Turret;
      var markerCount = layout?.MarkerTypes?.Length ?? 0;

      for (var i = 0; i < markerCount; i++) {
        if (layout.MarkerTypes[i] != typeInt || layout.MarkerTeams[i] != teamId)
          continue;

        turretIndex++;
        var turretEntity = frame.CreateEntity();
        frame.Add(turretEntity, new TransformComponent {
          Position = layout.MarkerPositions[i],
          Rotation = FP64.Zero,
          Scale = FPVector3.One
        });
        frame.Add(turretEntity, new Unit {
          UnitId = UnitIdGenerator.Next(ref frame),
          UnitTypeId = TurretUnitTypeId
        });
        frame.Add(turretEntity, new Team { TeamId = teamId });
        frame.Add(turretEntity, new Turret { TurretId = teamId * 100 + turretIndex });
        frame.Add(turretEntity, new Health {
          Current = StructureHealth,
          Max = StructureHealth
        });
        frame.Add(turretEntity, new Combat {
          AttackDamage = TurretAttackDamage,
          AttackRange = TurretAttackRange,
          AttackCooldownTicks = TurretAttackCooldownTicks,
          CooldownRemainingTicks = 0
        });
      }
    }
  }

  // Oases are neutral: no Team/Health/Combat/Unit, so they're structurally invisible to
  // TargetAcquisitionSystem and DamageSystem (both gate on Team+Health) and never move or attack.
  private static void SpawnOases(ref Frame frame, MapLayoutAsset layout) {
    var oasisIndex = 0;
    var typeInt = (int)MapMarkerType.Oasis;
    var markerCount = layout?.MarkerTypes?.Length ?? 0;

    for (var i = 0; i < markerCount; i++) {
      if (layout.MarkerTypes[i] != typeInt)
        continue;

      oasisIndex++;
      var oasisEntity = frame.CreateEntity();
      frame.Add(oasisEntity, new TransformComponent {
        Position = layout.MarkerPositions[i],
        Rotation = FP64.Zero,
        Scale = FPVector3.One
      });
      frame.Add(oasisEntity,
        new Oasis { OasisId = oasisIndex, SpawnCooldownRemainingMs = OasisSpawnSystem.SpawnIntervalMs });
    }
  }

  // Pickups are neutral like Oases: no Team/Health/Unit, so PickupSystem is the only thing that
  // ever touches them (proximity-based collect). MarkerValues carries the per-marker Amount
  // authored on the SimMarkerNode in the editor; missing values default to 0.
  private static void SpawnPickups(ref Frame frame, MapLayoutAsset layout) {
    var typeInt = (int)MapMarkerType.Pickup;
    var markerCount = layout?.MarkerTypes?.Length ?? 0;

    for (var i = 0; i < markerCount; i++) {
      if (layout.MarkerTypes[i] != typeInt)
        continue;

      var amount = layout.MarkerValues != null && i < layout.MarkerValues.Length ? layout.MarkerValues[i] : 0;
      var pickupEntity = frame.CreateEntity();
      frame.Add(pickupEntity, new TransformComponent {
        Position = layout.MarkerPositions[i],
        Rotation = FP64.Zero,
        Scale = FPVector3.One
      });
      frame.Add(pickupEntity, new Pickup { PickupId = PickupIdGenerator.Next(ref frame), Amount = amount });
    }
  }

  private static FPVector3 RequireMarkerPosition(MapLayoutAsset layout, MapMarkerType type, int teamId) {
    if (layout != null && layout.TryGetByTypeAndTeam(type, teamId, out var position))
      return position;

    throw new InvalidOperationException($"MapLayoutAsset is missing {type} marker for team {teamId}.");
  }
}
