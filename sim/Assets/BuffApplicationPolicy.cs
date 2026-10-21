namespace Meesles.Avalon.Sim.Assets;

public enum BuffIdentity {
  Skill = 0,
  SkillPerCaster = 1
}

public enum BuffReapplication {
  Refresh = 0,
  Stack = 1,
  Ignore = 2
}

public enum BuffDurationPolicy {
  Restart = 0,
  KeepLaterExpiry = 1,
  Extend = 2
}

public enum BuffStackOverflow {
  Reject = 0,
  RefreshOldest = 1,
  ReplaceWeakest = 2 // Weakness is defined by the effect type
}

public struct BuffApplicationPolicy {
  public BuffIdentity Identity;
  public BuffReapplication Reapplication;
  public BuffDurationPolicy Duration;
  public int MaxStacks;
  public BuffStackOverflow Overflow;

  public static BuffApplicationPolicy Default => new() { MaxStacks = 1 };

  public readonly bool IsValid =>
    (uint)Identity <= (uint)BuffIdentity.SkillPerCaster &&
    (uint)Reapplication <= (uint)BuffReapplication.Ignore &&
    (uint)Duration <= (uint)BuffDurationPolicy.Extend &&
    (uint)Overflow <= (uint)BuffStackOverflow.ReplaceWeakest && MaxStacks > 0;
}
