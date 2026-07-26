using System;
using System.Collections.Generic;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using Meesles.Avalon.Sim.Factories;
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
  public const int DefaultFactionId = AssetIds.FactionHairyWizards;

  public const int SetupGraceTicks = 30;

  public static void RegisterSystems(EcsSimulation simulation, NavigationRuntime navigation = null) {
    simulation.AddSystem(new CommandSystem(navigation), SystemPhase.Update);
    simulation.AddSystem(new HeroSpawnSystem(), SystemPhase.Update);
    simulation.AddSystem(new TeamPruneSystem(), SystemPhase.Update);
    simulation.AddSystem(new WaveSpawnSystem(), SystemPhase.Update);
    simulation.AddSystem(new InventorySystem(), SystemPhase.Update);
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
    frame.AssetRegistry.TryGet<MapLayoutAsset>(out var layout);

    var playerIds = GetPlayerIds(ref frame, maxPlayers);
    var structureTeams = spawnHeroesNow ? BuildRosterTeamIds(playerIds.Count) : GetAuthoredTeamIds(layout);

    SpawnTeamCrystalsAndSpawnPoints(ref frame, structureTeams, layout);
    SpawnHeroes(ref frame, playerIds, spawnHeroesNow);
    SpawnTeamTurrets(ref frame, structureTeams, layout);
    SpawnOases(ref frame, layout);
    SpawnPickups(ref frame, layout);
  }

  // ==================================================================================================================
  // =============================================== PRIVATE ==========================================================
  // ==================================================================================================================

  private static void SpawnTeamCrystalsAndSpawnPoints(ref Frame frame, List<int> teamIds, MapLayoutAsset layout) {
    var crystalStats = frame.AssetRegistry.Get<CrystalStatsAsset>();

    foreach (var teamId in teamIds) {
      var crystalEntity = frame.CreateEntity();
      var crystalPosition = RequireMarkerPosition(layout, MapMarkerType.Crystal, teamId);

      frame.Add(crystalEntity, TransformFactory.At(crystalPosition));
      frame.Add(crystalEntity, new Unit {
        UnitId = UnitIdGenerator.Next(ref frame),
        UnitTypeId = CrystalUnitTypeId
      });
      frame.Add(crystalEntity, new OwnerComponent { OwnerId = teamId });
      frame.Add(crystalEntity, new Team(teamId));
      frame.Add(crystalEntity, new Crystal { CrystalId = teamId });
      frame.Add(crystalEntity, new Health(crystalStats.Health));

      var spawnEntity = frame.CreateEntity();
      var spawnPosition = RequireMarkerPosition(layout, MapMarkerType.SpawnPoint, teamId);

      frame.Add(spawnEntity, TransformFactory.At(spawnPosition));
      frame.Add(spawnEntity, new Team(teamId));
      frame.Add(spawnEntity, new SpawnPoint {
        SpawnPointId = teamId,
        UnitTypeId = MinionUnitTypeId
      });
    }
  }

  private static void SpawnHeroes(ref Frame frame, List<int> playerIds, Boolean spawnHeroesNow) {
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
  }

  public static void SpawnHero(ref Frame frame, int playerId, int teamId, int factionId) {
    var playerStats = frame.AssetRegistry.Get<PlayerStatsAsset>();
    var combatStats = frame.AssetRegistry.Get<MinionStatsAsset>();

    var entity = frame.CreateEntity();
    var initialPos = GetHeroSpawnPositionForTeam(ref frame, teamId);

    frame.Add(entity, TransformFactory.At(initialPos));
    frame.Add(entity, new OwnerComponent { OwnerId = playerId });
    frame.Add(entity, new Player { PlayerId = playerId });
    frame.Add(entity, new Team(teamId));
    frame.Add(entity, new Faction(factionId));
    frame.Add(entity, new Hero(playerId));
    frame.Add(entity, new Unit {
      UnitId = UnitIdGenerator.Next(ref frame),
      UnitTypeId = PlayerUnitTypeId
    });
    frame.Add(entity, new Controllable());
    frame.Add(entity, new Inventory());
    frame.Add(entity, new Stats { Strength = combatStats != null ? combatStats.AttackDamage : 0 });

    if (playerStats != null)
      frame.Add(entity, new Health(playerStats.Health));

    if (combatStats != null)
      frame.Add(entity, new Combat(combatStats));

    frame.Add(entity, NavAgentFactory.At(initialPos, playerStats.MoveSpeed, playerStats.Radius));
  }

  private static void SpawnTeamTurrets(ref Frame frame, List<int> teamIds, MapLayoutAsset layout) {
    var turretStats = frame.AssetRegistry.Get<TurretStatsAsset>();

    foreach (var teamId in teamIds) {
      var turretIndex = 0;
      var typeInt = (int)MapMarkerType.Turret;
      var markerCount = layout?.MarkerTypes?.Length ?? 0;

      for (var i = 0; i < markerCount; i++) {
        if (layout.MarkerTypes[i] != typeInt || layout.MarkerTeams[i] != teamId)
          continue;

        turretIndex++;
        var turretEntity = frame.CreateEntity();
        frame.Add(turretEntity, TransformFactory.At(layout.MarkerPositions[i]));
        frame.Add(turretEntity, new Unit {
          UnitId = UnitIdGenerator.Next(ref frame),
          UnitTypeId = TurretUnitTypeId
        });
        frame.Add(turretEntity, new Team(teamId));
        frame.Add(turretEntity, new Turret { TurretId = teamId * 100 + turretIndex });
        frame.Add(turretEntity, new Health(turretStats.Health));
        frame.Add(turretEntity, new Stats { Strength = turretStats.AttackDamage });
        frame.Add(turretEntity, new Combat {
          AttackRange = turretStats.AttackRange,
          AttackCooldownTicks = turretStats.AttackCooldownTicks,
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
      frame.Add(oasisEntity, TransformFactory.At(layout.MarkerPositions[i]));
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
      frame.Add(pickupEntity, TransformFactory.At(layout.MarkerPositions[i]));
      frame.Add(pickupEntity, new Pickup { PickupId = PickupIdGenerator.Next(ref frame), Amount = amount });
    }
  }

  // 1-indexed list
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


  private static FPVector3 RequireMarkerPosition(MapLayoutAsset layout, MapMarkerType type, int teamId) {
    if (layout != null && layout.TryGetByTypeAndTeam(type, teamId, out var position))
      return position;

    throw new InvalidOperationException($"MapLayoutAsset is missing {type} marker for team {teamId}.");
  }
}
