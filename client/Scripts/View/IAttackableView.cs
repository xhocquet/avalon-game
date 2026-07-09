using Godot;

namespace Meesles.Avalon;

public interface IAttackableView {
  void OnAttackVfx(Vector3 targetPosition);
  void OnHitVfx(int damage, Vector3 attackerPosition);
}
