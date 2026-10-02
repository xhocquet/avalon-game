using Godot;

namespace Meesles.Avalon.Client.Scripts.Interfaces;

// Attack VFX starts on windup, before damage.
public interface IAttackableView {
  // Return false to use the debug fallback.
  bool OnAttackWindupVfx(Vector3 targetPosition, float windupSeconds);

  // The attack was canceled before landing.
  void OnAttackCanceledVfx();

  void OnHitVfx(float damage, Vector3 attackerPosition);
}
