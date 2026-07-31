namespace Meesles.Avalon.Sim;

public enum MapMarkerType {
  Crystal = 0,
  SpawnPoint = 1,
  Shop = 2,
  Turret = 3,
  Oasis = 4,
  Pickup = 5
}

public enum StatType {
  Strength = 0,
  GoldPerTick = 1,
  MoveSpeed = 2,
  AttackSpeed = 3,
  MaxHealth = 4
}

public enum MatchEndReason {
  Unknown = 0,
  Crystal = 1,
  Timeout = 2
}
