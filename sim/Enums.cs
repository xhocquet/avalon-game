using System;

namespace Meesles.Avalon.Sim;

public enum MapMarkerType {
  Crystal = 0,
  SpawnPoint = 1,
  Shop = 2,
  Turret = 3,
  Oasis = 4,
  Pickup = 5
}

// Indexes StatsComponent's value buffer directly, so the values must stay contiguous from 0 and
// StatRanges.Rows must carry one row per entry in the same order. Not serialized anywhere, so
// renumbering is safe. Keep StatCount last.
public enum StatType {
  MaxHealth = 0,
  MaxMana = 1,
  HealthRegen = 2, // Per 5 seconds, the unit these are authored in
  ManaRegen = 3, // Per 5 seconds
  Armor = 4,
  MagicResist = 5,
  AttackDamage = 6,
  BaseAttackSpeed = 7, // Attacks per second before bonuses
  BonusAttackSpeed = 8, // Fraction of base; 0.493 is +49.3%
  CritChance = 9, // 0-1
  CritDamage = 10, // Multiplier; 1.75 is 175%
  MoveSpeed = 11,
  AttackRange = 12,
  AcquisitionRange = 13,
  GameplayRadius = 14, // Body half-width every reach and hit test measures to

  StatCount = 15
}

// Which resist DamageApplication mitigates an incoming hit against.
public enum DamageType {
  Physical = 0,
  Magical = 1
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

// Authored in the asset JSON, so the values must stay stable.
public enum HeroBehavior {
  Default = 0
}

// Authored in the asset JSON, so the values must stay stable.
public enum HeroSkillSet {
  HairyWizard = 0,
  Shroom = 1,
  CrystalGiant = 2,
  Skinwalker = 3,
  PickleKnight = 4
}

// Test-only toggles a player can turn on for itself. Bitmask, carried on the wire by SetCheatCommand,
// so the values must stay stable. Keep in sync with Cheats.All.
[Flags]
public enum CheatFlags {
  None = 0,
  GodMode = 1 << 0 // Hero takes no damage
}

public enum MatchEndReason {
  Unknown = 0, // Unused
  Crystal = 1,
  Timeout = 2
}
