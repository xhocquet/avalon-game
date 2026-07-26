using Godot;
using Meesles.Avalon.Sim.Components;

namespace Meesles.Avalon.Client.Scripts.View;

internal static class CombatView {
  // Fraction of the attack cooldown that has elapsed, in [0, 1]. Returns 0 when no cooldown is set.
  public static float CooldownProgress(in Combat combat) {
    if (combat.AttackCooldownTicks <= 0) return 0f;

    var progress = 1f - (float)combat.CooldownRemainingTicks / combat.AttackCooldownTicks;
    return Mathf.Clamp(progress, 0f, 1f);
  }
}
