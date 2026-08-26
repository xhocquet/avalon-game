using Godot;

namespace Meesles.Avalon;

// A view that reacts to the attack phases the sim raises around it. Attack visuals hang off the
// windup, not the hit: the hit is the end of the swing, so a clip started there plays its wind-up
// after the damage it is meant to lead.
public interface IAttackableView {
  // True when the view played its own attack effect; false leaves the caller on the debug fallback.
  bool OnAttackWindupVfx(Vector3 targetPosition, float windupSeconds);

  // The swing this view started will not land - the target died or left range mid-wind-up.
  void OnAttackCanceledVfx();

  void OnHitVfx(float damage, Vector3 attackerPosition);
}
