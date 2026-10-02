// Launchable modes and their map data.

using System;
using Meesles.Avalon.Client.Scripts.GameState;

namespace Meesles.Avalon;

public static class GameTypeCatalog {
  public const string DefaultId = "avalon";

  public enum GameTypeMode {
    Networked,
    Local
  }

  public static readonly GameTypeDef[] GameTypes = [
    new("avalon", "Avalon", "Full match against the dedicated server.",
      GameTypeMode.Networked,
      Scenes.Multiplayer,
      Scenes.World,
      "res://Sim/Data/NavigationRegion3D.NavMeshData.bytes",
      "res://Sim/Data/MapLayout.bytes"),
    new("nav-playground", "Nav Playground", "Open grid with obstacles. Pathing and movement.",
      GameTypeMode.Local,
      Scenes.Singleplayer,
      Scenes.NavPlayground,
      "res://Sim/Data/NavPlayground.NavMeshData.bytes",
      "res://Sim/Data/MapLayout_NavPlayground.bytes"),
    new("combat-playground", "Combat Playground", "Four bases with shops and cover. Heroes, skills and combat.",
      GameTypeMode.Local,
      Scenes.CombatPlayground,
      Scenes.CombatArena,
      "res://Sim/Data/CombatArena.NavMeshData.bytes",
      "res://Sim/Data/MapLayout_CombatArena.bytes")
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

    public readonly string GameScenePath = gameScenePath;

    // Instanced as "World".
    public readonly string WorldScenePath = worldScenePath;
    public readonly string NavMeshPath = navMeshPath;
    public readonly string MapLayoutPath = mapLayoutPath;

    public bool IsLocal => Mode == GameTypeMode.Local;
  }
}
