using System;
using Meesles.Avalon.Sim;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.Godot;

namespace Meesles.Avalon.Client.Scripts.View;

public class VfxManager {
  private IDisposable _attackHitSub;
  private EntityViewUpdaterNode _view;

  public void Attach(SimEventHub events, EntityViewUpdaterNode view) {
    Detach();
    _view = view;
    // Predicted (not confirmed) so hits flash immediately on the local tick; a mispredicted
    // flash is transient and harmless, which is why UI state uses confirmed events but VFX do not.
    _attackHitSub = events.OnPredicted<AttackHitEvent>(HandleAttackHit);
  }

  public void Detach() {
    _attackHitSub?.Dispose();
    _attackHitSub = null;
    _view = null;
  }

  private void HandleAttackHit(AttackHitEvent evt) {
    if (_view == null) return;

    var views = _view.ViewsByUnitId;
    views.TryGetValue(evt.AttackerUnitId, out var attackerView);
    views.TryGetValue(evt.TargetUnitId, out var targetView);

    var attackerPos = attackerView?.GlobalPosition ?? evt.AttackerPosition.ToVector3();
    var targetPos = targetView?.GlobalPosition ?? evt.TargetPosition.ToVector3();

    var line = DebugAttackLine.Create(attackerPos, targetPos);
    _view.AddChild(line);

    if (attackerView is IAttackableView attacker)
      attacker.OnAttackVfx(targetPos);
    if (targetView is IAttackableView target)
      target.OnHitVfx(evt.Damage, attackerPos);
  }
}
