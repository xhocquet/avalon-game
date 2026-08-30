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
  public const int StatBuffs = 135;
  public const int AttackProc = 136;
  public const int AttackHitIdCounter = 137;
  public const int AttackBurst = 138;
  public const int Snare = 139;
  public const int SkillCharge = 140;
  public const int DamageOverTime = 141;

  // Next free id: 142
}
