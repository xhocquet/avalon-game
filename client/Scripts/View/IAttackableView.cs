using Godot;

namespace Meesles.Avalon;

public interface IAttackableView {
  // True when the view played its own attack effect; false leaves the caller on the debug fallback.
  bool OnAttackVfx(Vector3 targetPosition);
  void OnHitVfx(float damage, Vector3 attackerPosition);
}
