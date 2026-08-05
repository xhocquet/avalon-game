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

// Which of a hero's four skill slots a command or event refers to. The values are indices into
// SkillsComponent's fixed buffers and into HeroAsset.Skill1..4AssetId, so they must stay 0-based and
// contiguous.
public enum SkillSlot {
  HardHit = 0,
  Buff = 1,
  RangeShot = 2,
  Ultimate = 3
}

public enum MatchEndReason {
  Unknown = 0,
  Crystal = 1,
  Timeout = 2
}
