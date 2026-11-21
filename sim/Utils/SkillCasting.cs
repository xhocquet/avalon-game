using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim;

public static class SkillCasting {
  public static bool IsActive(ref Frame frame, EntityRef entity) =>
    (frame.Has<SkillCharge>(entity) && frame.GetReadOnly<SkillCharge>(entity).IsCharging) ||
    (frame.Has<SkillChannel>(entity) && frame.GetReadOnly<SkillChannel>(entity).IsActive);
}
