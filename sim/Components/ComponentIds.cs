namespace Meesles.Avalon.Sim.Components;

// Central ledger of Klotho component network ids. These ids are baked into snapshots and the wire
// format, so they must stay stable across builds: never renumber a live component, and never reuse
// the id of a deleted one. Allocate from "next free" at the bottom.
//
// Kept in numeric order (not grouped by file) so gaps and duplicate ids are visible at a glance.
public static class ComponentIds {
  public const int Player = 100;
  public const int UnitIdentity = 101;
  public const int Team = 102;
  public const int Health = 103;
  public const int Hero = 104;
  public const int Minion = 105;
  public const int Crystal = 106;
  public const int SpawnPoint = 107;
  public const int Combat = 108;
  public const int UnitIdCounter = 109;
  public const int UnitMoveTarget = 110;
  public const int AttackTargetUnitId = 111;
  public const int PendingRespawn = 112;
  public const int Turret = 113;
  public const int Controllable = 114;
  public const int Faction = 115;
  public const int PlayerFaction = 116;
  public const int Oasis = 117;
  public const int Inventory = 118;
  public const int Stats = 119;
  public const int Pickup = 120;
  public const int OasisEjectPending = 121;
  public const int OasisResourceLanding = 122;
  public const int PickupIdCounter = 123;
  public const int MatchSetupState = 124;
  public const int MinionSettleTracker = 125;
  public const int NavSnapTracker = 126;
  public const int Experience = 127;
  public const int Respawns = 128;
  public const int Skills = 129;
  public const int Projectile = 130;
  public const int ProjectileIdCounter = 131;
  public const int CheatState = 132;
  public const int MatchOutcome = 133;
  public const int Resources = 134;
  // 135 retired: StatBuffs
  // 136 retired: AttackProc without start tick
  public const int AttackHitIdCounter = 137;
  // 138 retired: AttackBurst without start tick
  // 139 retired: Snare without start tick
  public const int SkillCharge = 140;
  // 141 retired: single-slot DamageOverTime
  public const int TrailEmitter = 142;
  public const int TrailSegment = 143;
  public const int TrailSegmentIdCounter = 144;
  // 145 retired: Silence without start tick
  public const int SkillChannel = 146;
  public const int SkillDash = 147;
  public const int SkillStockpiles = 148;

  public const int UnitVisibility = 149;

  // 150 retired: Buffs without caster attribution
  // 151 retired: BuffChanges with applied deltas

  public const int DamageOverTime = 152;

  public const int Buffs = 153;

  public const int BaseStats = 154;
  public const int BuffChanges = 155;

  public const int AttackProc = 156;
  public const int AttackBurst = 157;
  public const int Snare = 158;
  public const int Silence = 159;

  // Next free id: 160
}
