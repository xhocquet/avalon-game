using System;
using System.Collections.Generic;
using Meesles.Avalon.Sim.Assets;

namespace Meesles.Avalon.Sim;

public static class SimulationCapacity {
  public const int PlayerSlots = 4; // Process-wide component layout, shared by local and networked modes.
  public const int OasisSlots = 1; // Live fountains; each carries at most one pending ejection and landing.
  public const int TurretsPerPlayer = 5;
  public const int CrystalsPerPlayer = 1;
  public const int SpawnPointsPerPlayer = 1;
  public const int TurretSlots = TurretsPerPlayer * PlayerSlots;
  public const int CrystalSlots = CrystalsPerPlayer * PlayerSlots;
  public const int SpawnPointSlots = SpawnPointsPerPlayer * PlayerSlots;
  public const int PickupSlots = 128;

  public static void ValidatePlayerCount(int maxPlayers) {
    if (maxPlayers < 1 || maxPlayers > PlayerSlots)
      throw new ArgumentOutOfRangeException(nameof(maxPlayers), maxPlayers,
        $"Player count must be between 1 and {PlayerSlots}.");
  }

  public static void ValidateMapLayout(MapLayoutAsset layout) {
    var oasisCount = 0;
    var pickupCount = 0;
    for (var i = 0; i < (layout?.MarkerCount ?? 0); i++) {
      if (layout.MarkerTypes[i] == (int)MapMarkerType.Oasis)
        oasisCount++;
      if (layout.MarkerTypes[i] == (int)MapMarkerType.Pickup)
        pickupCount++;
    }
    if (oasisCount > OasisSlots)
      throw new InvalidOperationException(
        $"Map '{layout.MapName}' has {oasisCount} oasis markers; capacity is {OasisSlots}.");
    if (pickupCount > PickupSlots)
      throw new InvalidOperationException(
        $"Map '{layout.MapName}' has {pickupCount} pickup markers; capacity is {PickupSlots}.");
    ValidateStructures(layout, MapMarkerType.Turret, TurretsPerPlayer, TurretSlots);
    ValidateStructures(layout, MapMarkerType.Crystal, CrystalsPerPlayer, CrystalSlots);
    ValidateStructures(layout, MapMarkerType.SpawnPoint, SpawnPointsPerPlayer, SpawnPointSlots);
  }

  private static void ValidateStructures(MapLayoutAsset layout, MapMarkerType type, int perPlayer, int capacity) {
    var counts = new Dictionary<int, int>();
    var total = 0;
    for (var i = 0; i < (layout?.MarkerCount ?? 0); i++) {
      var teamId = layout.MarkerTeams[i];
      if (layout.MarkerTypes[i] != (int)type || teamId <= 0)
        continue;
      var count = counts.TryGetValue(teamId, out var previous) ? previous + 1 : 1;
      counts[teamId] = count;
      total++;
      if (count > perPlayer)
        throw new InvalidOperationException(
          $"Map '{layout.MapName}' has {count} {type} markers for team {teamId}; per-player capacity is {perPlayer}.");
    }
    if (total > capacity)
      throw new InvalidOperationException(
        $"Map '{layout.MapName}' has {total} {type} markers; capacity is {capacity}.");
  }
}
