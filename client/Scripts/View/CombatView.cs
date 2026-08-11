using Godot;
using Meesles.Avalon.Sim;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Client.Scripts.View;

internal static class CombatView {
  // Fraction of the attack cooldown that has elapsed, in [0, 1]. Returns 0 when no cooldown is set.
  // The period comes from CombatTiming rather than the component, because the sim derives it from
  // attack speed at the moment of the hit instead of storing it.
  public static float CooldownProgress(Frame frame, EntityRef attacker) {
    if (!frame.Has<Combat>(attacker)) return 0f;

    var total = CombatTiming.CooldownTicks(ref frame, attacker);
    if (total <= 0) return 0f;

    var remaining = frame.GetReadOnly<Combat>(attacker).CooldownRemainingTicks;
    return Mathf.Clamp(1f - (float)remaining / total, 0f, 1f);
  }
}
