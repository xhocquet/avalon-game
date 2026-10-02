namespace Meesles.Avalon.Sim;

// Klotho simulation-event wire ids. Keep assigned ids stable and do not reuse the gaps.
public static class SimulationEventTypeIds {
  public const int GameOver = 101;
  public const int UnitDied = 102;
  public const int PlayerDied = 104;
  public const int PlayerRespawned = 105;
  public const int CrystalDestroyed = 106;
  public const int TurretDestroyed = 107;
  public const int AttackHit = 108;
  public const int OasisResourcePreparing = 109;
  public const int OasisResourceEjected = 110;
  public const int OasisResourceLanded = 111;
  public const int TeamPruned = 112;
  public const int HeroLeveledUp = 113;
  public const int SkillUpgraded = 114;
  public const int SkillCast = 115;
  public const int SkillProjectileSpawned = 116;
  public const int SkillProjectileDespawned = 117;
  public const int AttackProcConsumed = 118;
  public const int SkillChargeDetonated = 119;
  public const int AttackWindupStarted = 120;
  public const int AttackWindupCanceled = 121;
  public const int SkillTrailSegmentSpawned = 122;

  // Next free id: 123
}
