// The maps/modes the lobby can launch. Each entry names the scene to change to plus the map data
// that scene's sim has to load — a map is a world scene, a navmesh export and a MapLayout export
// that were all produced from the same .tscn by AvalonBuildExportRunner.

using System;
using Meesles.Avalon.Client.Scripts.View;

namespace Meesles.Avalon;

public static class GameTypeCatalog {
  public const string DefaultId = "avalon";

  // Networked types join the dedicated server, which loads its own copy of the map data at startup
  // and only ever serves the Avalon map. Local types host the sim in-process, which is the only
  // reason a playground can pick its own navmesh and layout.
  public enum GameTypeMode {
    Networked,
    Local
  }

  public static readonly GameTypeDef[] GameTypes = [
    new("avalon", "Avalon", "Full match against the dedicated server.",
      GameTypeMode.Networked,
      "res://Scenes/Multiplayer.tscn",
      "res://Scenes/World/World.tscn",
      "res://Sim/Data/NavigationRegion3D.NavMeshData.bytes",
      "res://Sim/Data/MapLayout.bytes"),
    new("nav-playground", "Nav Playground", "Open grid with obstacles. Pathing and movement.",
      GameTypeMode.Local,
      "res://Scenes/Singleplayer.tscn",
      "res://Scenes/Playgrounds/NavPlayground.tscn",
      "res://Sim/Data/NavPlayground.NavMeshData.bytes",
      "res://Sim/Data/MapLayout_NavPlayground.bytes"),
    new("combat-playground", "Combat Playground", "Avalon map, hosted locally. Combat and skills.",
      GameTypeMode.Local,
      "res://Scenes/Singleplayer.tscn",
      "res://Scenes/Playgrounds/CombatPlayground.tscn",
      "res://Sim/Data/CombatPlayground.NavMeshData.bytes",
      "res://Sim/Data/MapLayout_CombatPlayground.bytes")
  ];

  public static GameTypeDef Selected => Resolve(GameTypeSelection.SelectedGameTypeId);

  public static GameTypeDef Resolve(string id) {
    foreach (var def in GameTypes)
      if (string.Equals(def.Id, id, StringComparison.OrdinalIgnoreCase))
        return def;

    return GameTypes[0];
  }

  public static bool Exists(string id) {
    foreach (var def in GameTypes)
      if (string.Equals(def.Id, id, StringComparison.OrdinalIgnoreCase))
        return true;

    return false;
  }

  public readonly struct GameTypeDef(
    string id,
    string name,
    string description,
    GameTypeMode mode,
    string gameScenePath,
    string worldScenePath,
    string navMeshPath,
    string mapLayoutPath) {
    public readonly string Id = id;
    public readonly string Name = name;
    public readonly string Description = description;
    public readonly GameTypeMode Mode = mode;

    // Scene the lobby hands off to: the networked game node, or the local one.
    public readonly string GameScenePath = gameScenePath;

    // Instanced as "World" by the game node at runtime, so one game scene serves every map.
    public readonly string WorldScenePath = worldScenePath;
    public readonly string NavMeshPath = navMeshPath;
    public readonly string MapLayoutPath = mapLayoutPath;

    public bool IsLocal => Mode == GameTypeMode.Local;
  }
}
