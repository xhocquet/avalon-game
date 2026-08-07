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
  MaxHealth = 4,
  Defense = 5
}

public enum SkillSlot {
  Primary = 0,
  Secondary = 1,
  Tertiary = 2,
  Ultimate = 3
}

// Why a skill projectile left the board. Rides SkillProjectileDespawnedEvent as an int.
public enum SkillProjectileEnd {
  Hit = 1,
  Expired = 2
}

// Authored in Assets.json, so the values must stay stable.
public enum HeroBehavior {
  Default = 0
}

// Authored in Assets.json, so the values must stay stable.
public enum HeroSkillSet {
  HairyWizard = 0,
  Shroom = 1,
  CrystalGiant = 2,
  Skinwalker = 3,
  PickleKnight = 4
}

public enum MatchEndReason {
  Unknown = 0, // Unused
  Crystal = 1,
  Timeout = 2
}
