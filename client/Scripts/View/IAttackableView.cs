using Godot;

namespace Meesles.Avalon;

public interface IAttackableView {
  void OnAttackVfx(Vector3 targetPosition);
  void OnHitVfx(float damage, Vector3 attackerPosition);
}
