using System;
using System.Collections.Generic;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using Meesles.Avalon.Sim.Factories;
using Meesles.Avalon.Sim.Navigation;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.ECS.Systems;

namespace Meesles.Avalon.Sim;

public static class SimulationSetup {
  public const int PlayerUnitTypeId = 1;
  public const int MinionUnitTypeId = 2;
  public const int CrystalUnitTypeId = 100;
  public const int TurretUnitTypeId = 101;
  public const int DefaultFactionId = AssetIds.FactionHairyWizards;

  // How long setup waits on faction picks before proceeding with whatever is on the board. Shared
  // by HeroSpawnSystem and TeamPruneSystem so they agree on when setup is over.
  public static int GetSetupGraceTicks(ref Frame frame) {
    return frame.AssetRegistry.Get<MatchRulesAsset>().SetupGraceTicks;
  }

  // Order notes -
  // Systems process in order they are defined. This means certain ordering is intentional:
  // DeathSystem processes after all damage for the frame, so you get immediate feedback (and rewards)
  public static void RegisterSystems(EcsSimulation simulation, NavigationRuntime navigation = null) {
    // Bookkeeping
    simulation.AddSystem(new TeamPruneSystem(), SystemPhase.Update);
    simulation.AddSystem(new RespawnSystem(), SystemPhase.Update);
    simulation.AddSystem(new HeroSpawnSystem(), SystemPhase.Update);
    simulation.AddSystem(new WaveSpawnSystem(), SystemPhase.Update);
    simulation.AddSystem(new OasisSpawnSystem(), SystemPhase.Update);

    // Command intake is not what this slot buys: EcsSimulation.Tick drains every OnCommand ahead of the
    // whole Update phase, so orders had already landed before the first system above ran. What sits here
    // is CommandSystem.Update, the transform integrator for anything NavigationAgentSystem won't carry —
    // every unit when navigation is null, otherwise only move targets held by non-nav agents. It sits
    // beside the nav registration below so both movement paths land at the same point in the frame.
    simulation.AddSystem(new CommandSystem(navigation), SystemPhase.Update);

    // Hero behaviors and items will impact stats and other system
    simulation.AddSystem(new HeroBehaviorSystem(), SystemPhase.Update);
    simulation.AddSystem(new InventorySystem(), SystemPhase.Update);

    if (navigation != null) // Movement
      simulation.AddSystem(new NavigationAgentSystem(navigation), SystemPhase.Update);

    simulation.AddSystem(new PickupSystem(), SystemPhase.Update); // Depends on movement

    // Begin offensive concepts
    simulation.AddSystem(new TargetAcquisitionSystem(), SystemPhase.Update);
    simulation.AddSystem(new SkillSystem(), SystemPhase.Update);
    simulation.AddSystem(new ProjectileSystem(), SystemPhase.Update);
    simulation.AddSystem(new AttackIntentSystem(), SystemPhase.Update);
    simulation.AddSystem(new AttackCooldownSystem(), SystemPhase.Update);
    simulation.AddSystem(new DamageSystem(), SystemPhase.Update);
    simulation.AddSystem(new DeathSystem(), SystemPhase.Update);

    // End of frame phase
    simulation.AddSystem(new ExperienceSystem(), SystemPhase.Update);
    simulation.AddSystem(new ScoreSystem(), SystemPhase.LateUpdate);
    simulation.AddSystem(new EventSystem(), SystemPhase.LateUpdate);
  }

  public static void InitializeWorld(IKlothoEngine engine, int maxPlayers) {
    var frame = engine.PredictedFrame.Frame;
    InitializeWorld(ref frame, maxPlayers);
  }

  // spawnHeroesNow: use default factions or selected ones
  public static void InitializeWorld(ref Frame frame, int maxPlayers, bool spawnHeroesNow = false) {
    UnitLookup.InitializeUnitIds(ref frame);
    frame.AssetRegistry.TryGet<MapLayoutAsset>(out var layout);

    var playerIds = GetPlayerIds(ref frame, maxPlayers);
    var structureTeams = spawnHeroesNow ? BuildRosterTeamIds(playerIds.Count) : GetAuthoredTeamIds(layout);

    SpawnTeamCrystalsAndSpawnPoints(ref frame, structureTeams, layout);
    SpawnHeroes(ref frame, playerIds, spawnHeroesNow);
    SpawnTeamTurrets(ref frame, structureTeams, layout);
    SpawnOases(ref frame, layout);
    SpawnPickups(ref frame, layout);
  }

  private static void SpawnTeamCrystalsAndSpawnPoints(ref Frame frame, List<int> teamIds, MapLayoutAsset layout) {
    var crystalStats = frame.AssetRegistry.Get<CrystalStatsAsset>();

    foreach (var teamId in teamIds) {
      var crystalPosition = RequireMarkerPosition(layout, MapMarkerType.Crystal, teamId);
      CrystalFactory.Spawn(ref frame, crystalStats, crystalPosition, teamId);

      var spawnPosition = RequireMarkerPosition(layout, MapMarkerType.SpawnPoint, teamId);
      SpawnPointFactory.Spawn(ref frame, spawnPosition, teamId);
    }
  }

  private static void SpawnHeroes(ref Frame frame, List<int> playerIds, bool spawnHeroesNow) {
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
    var heroAsset = GetHeroAssetForFaction(ref frame, factionId);
    var matchRules = frame.AssetRegistry.Get<MatchRulesAsset>();
    var initialPos = GetHeroSpawnPositionForTeam(ref frame, teamId);

    HeroFactory.Spawn(ref frame, heroAsset, matchRules, initialPos, playerId, teamId, factionId);
  }

  private static HeroAsset GetHeroAssetForFaction(ref Frame frame, int factionId) {
    var faction = frame.AssetRegistry.Get<FactionAsset>(factionId);
    if (frame.AssetRegistry.TryGet<HeroAsset>(faction.HeroAssetId, out var heroAsset))
      return heroAsset;

    throw new InvalidOperationException(
      $"FactionAsset {factionId} names HeroAssetId {faction.HeroAssetId}, which is not in Assets.bytes.");
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
        TurretFactory.Spawn(ref frame, turretStats, layout.MarkerPositions[i], teamId, turretIndex);
      }
    }
  }

  private static void SpawnOases(ref Frame frame, MapLayoutAsset layout) {
    var oasisIndex = 0;
    var typeInt = (int)MapMarkerType.Oasis;
    var markerCount = layout?.MarkerTypes?.Length ?? 0;
    var initialCooldownMs = frame.AssetRegistry.Get<PickupRulesAsset>().OasisSpawnIntervalMs;

    for (var i = 0; i < markerCount; i++) {
      if (layout.MarkerTypes[i] != typeInt)
        continue;

      oasisIndex++;
      var oasisEntity = frame.CreateEntity();
      frame.Add(oasisEntity, TransformFactory.At(layout.MarkerPositions[i]));
      frame.Add(oasisEntity,
        new Oasis { OasisId = oasisIndex, SpawnCooldownRemainingMs = initialCooldownMs });
    }
  }

  private static void SpawnPickups(ref Frame frame, MapLayoutAsset layout) {
    var typeInt = (int)MapMarkerType.Pickup;
    var markerCount = layout?.MarkerTypes?.Length ?? 0;

    for (var i = 0; i < markerCount; i++) {
      if (layout.MarkerTypes[i] != typeInt)
        continue;

      var amount = layout.MarkerValues != null && i < layout.MarkerValues.Length ? layout.MarkerValues[i] : 0;
      var pickupEntity = frame.CreateEntity();
      frame.Add(pickupEntity, TransformFactory.At(layout.MarkerPositions[i]));
      frame.Add(pickupEntity, new Pickup { PickupId = IdCounter<PickupIdCounter>.Next(ref frame), Amount = amount });
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
