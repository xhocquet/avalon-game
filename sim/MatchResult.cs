using System;
using System.Collections.Generic;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim;

// One resource kind on a player's line. Only the kinds the round actually used get a row, so a
// scoreboard never has to render a column of zeros for a type this map never spawned.
public readonly struct ResourceTally {
  public ResourceTally(int typeAssetId, int count) {
    TypeAssetId = typeAssetId;
    Count = count;
  }

  public int TypeAssetId { get; }
  public int Count { get; }
}

// A resource kind the round used, and what was feeding it.
public readonly struct ResourceTypeSummary {
  public ResourceTypeSummary(int typeAssetId, int amountPerPickup, int oasisCount) {
    TypeAssetId = typeAssetId;
    AmountPerPickup = amountPerPickup;
    OasisCount = oasisCount;
  }

  public int TypeAssetId { get; }
  public int AmountPerPickup { get; }
  public int OasisCount { get; }
}

// The setup the round was played under, read off the frame at the moment it ended. Everything here
// is sim-side; wall-clock time and the network seed belong to whoever is writing the record out.
public readonly struct MatchContext {
  public string MapName { get; init; }
  public int TickIntervalMs { get; init; }
  public int MatchDurationSec { get; init; } // the timeout limit, not how long this match ran
  public int ContenderTeamCount { get; init; }
  public ResourceTypeSummary[] ResourceTypes { get; init; }
}

// One player's line on the end-of-match scoreboard, read off their hero at the moment the match ended.
public readonly struct PlayerResult {
  public int PlayerId { get; init; }

  // Off the sim path - the join handshake carries it, so it is only set when the caller passes a
  // name lookup. Null on every peer that has no roster to resolve it against.
  public string Name { get; init; }

  public int TeamId { get; init; }
  public int FactionId { get; init; }
  public int HeroAssetId { get; init; }
  public bool IsWinner { get; init; }
  public int Score { get; init; }
  public int HeroKills { get; init; }
  public int Deaths { get; init; }
  public int MinionKills { get; init; }
  public int StructureKills { get; init; }
  public int DamageDealt { get; init; }
  public int Level { get; init; }
  public int Gold { get; init; }

  // One row per kind in MatchContext.ResourceTypes, same order.
  public ResourceTally[] Resources { get; init; }

  public int TotalResources { get; init; }
}

public readonly struct MatchResult {
  public const int NoWinnerPlayerId = -1;
  public const int NoWinnerTeamId = MatchOutcome.NoWinnerTeamId;

  public int EndTick { get; init; }
  public int DurationMs { get; init; }
  public int WinnerPlayerId { get; init; }
  public int WinnerTeamId { get; init; }
  public MatchEndReason Reason { get; init; }
  public MatchContext Context { get; init; }
  public PlayerResult[] Players { get; init; }

  // The team is the outcome. A win whose player id could not be resolved is still a win.
  public bool HasWinner => WinnerTeamId != NoWinnerTeamId;
  public bool IsDraw => !HasWinner;
}

public static class MatchResultReader {
  private static readonly PlayerResult[] NoPlayers = [];
  private static readonly ResourceTypeSummary[] NoResourceTypes = [];
  private static readonly ResourceTally[] NoResources = [];

  // nameLookup resolves a player id to a display name; pass null where no roster is available.
  public static bool TryRead(ref Frame frame, out MatchResult result,
    Func<int, string> nameLookup = null) {
    result = default;

    if (!frame.TryGetSingleton<MatchOutcome>(out var outcomeEntity))
      return false;

    ref readonly var outcome = ref frame.GetReadOnly<MatchOutcome>(outcomeEntity);
    if (!outcome.Ended)
      return false;

    // Klotho's own end state is where the single-winner player id lives; MatchOutcome owns the team.
    var winnerPlayerId = MatchResult.NoWinnerPlayerId;
    if (frame.TryGetSingleton<MatchEndStateComponent>(out var matchEndEntity))
      winnerPlayerId = frame.GetReadOnly<MatchEndStateComponent>(matchEndEntity).WinnerPlayerId;

    var resourceTypes = ReadResourceTypes(ref frame);
    result = new MatchResult {
      EndTick = outcome.EndTick,
      DurationMs = outcome.EndTick * TickMath.DeltaTimeMs(ref frame),
      WinnerPlayerId = winnerPlayerId,
      WinnerTeamId = outcome.WinnerTeamId,
      Reason = (MatchEndReason)outcome.Reason,
      Context = ReadContext(ref frame, resourceTypes),
      Players = ReadPlayers(ref frame, outcome.WinnerTeamId, resourceTypes, nameLookup)
    };
    return true;
  }

  private static MatchContext ReadContext(ref Frame frame, ResourceTypeSummary[] resourceTypes) {
    var rules = frame.AssetRegistry.Get<MatchRulesAsset>();
    var contenderTeamCount = frame.TryGetSingleton<MatchSetupState>(out var setupEntity)
      ? frame.GetReadOnly<MatchSetupState>(setupEntity).ContenderTeamCount
      : 0;

    return new MatchContext {
      MapName = frame.AssetRegistry.TryGet<MapLayoutAsset>(out var layout) ? layout.MapName : null,
      TickIntervalMs = TickMath.DeltaTimeMs(ref frame),
      MatchDurationSec = rules.MatchDuration.ToInt(),
      ContenderTeamCount = contenderTeamCount,
      ResourceTypes = resourceTypes
    };
  }

  // What the round actually spawned: every kind an oasis ejects, plus any kind a player is holding
  // (hand-placed pickups have no oasis behind them and would otherwise leave an unlabelled tally).
  private static ResourceTypeSummary[] ReadResourceTypes(ref Frame frame) {
    var oasisCounts = new int[PickupTypes.MaxTypes];
    var used = new bool[PickupTypes.MaxTypes];

    var oasisFilter = frame.Filter<Oasis>();
    while (oasisFilter.Next(out var oasisEntity)) {
      var slot = PickupTypes.SlotOf(frame.GetReadOnly<Oasis>(oasisEntity).PickupTypeAssetId);
      if (slot == PickupTypes.InvalidSlot)
        continue;

      oasisCounts[slot]++;
      used[slot] = true;
    }

    var walletFilter = frame.Filter<ResourcesComponent>();
    while (walletFilter.Next(out var walletEntity)) {
      ref readonly var wallet = ref frame.GetReadOnly<ResourcesComponent>(walletEntity);
      for (var slot = 0; slot < PickupTypes.MaxTypes; slot++)
        if (wallet.GetSlot(slot) > 0)
          used[slot] = true;
    }

    var types = new List<ResourceTypeSummary>();
    for (var slot = 0; slot < PickupTypes.MaxTypes; slot++) {
      if (!used[slot])
        continue;

      var typeAssetId = PickupTypes.AssetIdOf(slot);
      var amount = frame.AssetRegistry.TryGet<PickupTypeAsset>(typeAssetId, out var type) ? type.Amount : 0;
      types.Add(new ResourceTypeSummary(typeAssetId, amount, oasisCounts[slot]));
    }

    return types.Count > 0 ? types.ToArray() : NoResourceTypes;
  }

  private static PlayerResult[] ReadPlayers(ref Frame frame, int winnerTeamId,
    ResourceTypeSummary[] resourceTypes, Func<int, string> nameLookup) {
    var players = new List<PlayerResult>();

    var filter = frame.Filter<Hero, Player, TeamComponent>();
    while (filter.Next(out var entity)) {
      ref readonly var hero = ref frame.GetReadOnly<Hero>(entity);
      ref readonly var record = ref frame.GetReadOnly<Player>(entity);
      var teamId = frame.GetReadOnly<TeamComponent>(entity).TeamId;
      var resources = ReadResources(ref frame, entity, resourceTypes, out var totalResources);

      players.Add(new PlayerResult {
        PlayerId = hero.PlayerId,
        Name = nameLookup?.Invoke(hero.PlayerId),
        TeamId = teamId,
        FactionId = frame.Has<FactionComponent>(entity)
          ? frame.GetReadOnly<FactionComponent>(entity).FactionId
          : 0,
        HeroAssetId = hero.HeroAssetId,
        IsWinner = teamId == winnerTeamId,
        Score = record.Score,
        HeroKills = record.HeroKills,
        Deaths = record.Deaths,
        MinionKills = record.MinionKills,
        StructureKills = record.StructureKills,
        DamageDealt = FP64.Round(record.DamageDealt).ToInt(), // Whole numbers on the scoreboard
        Level = frame.Has<ExperienceComponent>(entity)
          ? frame.GetReadOnly<ExperienceComponent>(entity).Level
          : 0,
        Gold = frame.Has<InventoryComponent>(entity)
          ? frame.GetReadOnly<InventoryComponent>(entity).Gold
          : 0,
        Resources = resources,
        TotalResources = totalResources
      });
    }

    if (players.Count == 0)
      return NoPlayers;

    // Entity iteration order is an implementation detail; the scoreboard wants a stable one.
    players.Sort(static (a, b) => a.TeamId != b.TeamId
      ? a.TeamId.CompareTo(b.TeamId)
      : a.PlayerId.CompareTo(b.PlayerId));
    return players.ToArray();
  }

  private static ResourceTally[] ReadResources(ref Frame frame, EntityRef entity,
    ResourceTypeSummary[] resourceTypes, out int total) {
    total = 0;
    if (resourceTypes.Length == 0 || !frame.Has<ResourcesComponent>(entity))
      return NoResources;

    ref readonly var wallet = ref frame.GetReadOnly<ResourcesComponent>(entity);
    var tallies = new ResourceTally[resourceTypes.Length];
    for (var i = 0; i < resourceTypes.Length; i++) {
      var count = wallet.CountOf(resourceTypes[i].TypeAssetId);
      tallies[i] = new ResourceTally(resourceTypes[i].TypeAssetId, count);
      total += count;
    }

    return tallies;
  }
}
