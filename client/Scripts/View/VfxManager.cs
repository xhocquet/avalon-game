using System;
using Godot;
using Meesles.Avalon.Sim;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.Godot;

namespace Meesles.Avalon {
  public class VfxManager {
    private IKlothoEngine _engine;
    private EntityViewUpdaterNode _view;
    private Action<int, SimulationEvent> _onEventPredicted;

    public void Attach(IKlothoEngine engine, EntityViewUpdaterNode view) {
      Detach();
      _engine = engine;
      _view = view;
      _onEventPredicted = OnEventPredicted;
      _engine.OnEventPredicted += _onEventPredicted;
    }

    public void Detach() {
      if (_engine != null && _onEventPredicted != null)
        _engine.OnEventPredicted -= _onEventPredicted;
      _onEventPredicted = null;
      _engine = null;
      _view = null;
    }

    private void OnEventPredicted(int tick, SimulationEvent evt) {
      if (evt is AttackHitEvent attack)
        HandleAttackHit(attack);
    }

    private void HandleAttackHit(AttackHitEvent evt) {
      if (_view == null) return;

      var views = _view.ViewsByUnitId;
      views.TryGetValue(evt.AttackerUnitId, out var attackerView);
      views.TryGetValue(evt.TargetUnitId, out var targetView);

      Vector3 attackerPos = attackerView?.GlobalPosition ?? evt.AttackerPosition.ToVector3();
      Vector3 targetPos = targetView?.GlobalPosition ?? evt.TargetPosition.ToVector3();

      var line = DebugAttackLine.Create(attackerPos, targetPos);
      _view.AddChild(line);

      if (attackerView is IAttackableView attacker)
        attacker.OnAttackVfx(targetPos);
      if (targetView is IAttackableView target)
        target.OnHitVfx(evt.Damage, attackerPos);
    }
  }
}
