using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim;

// Raises AttackWindupStartedEvent and AttackWindupCanceledEvent
// AttackHitEvent, is raised by DamageController to support skills
public static class AttackPhases {
  public static void RaiseWindupStarted(ref Frame frame, EntityRef attacker, EntityRef target,
    int targetUnitId, int attackHitId, int windupTicks) {
    if (frame.EventRaiser == null)
      return;

    var evt = EventPool.Get<AttackWindupStartedEvent>();
    evt.AttackHitId = attackHitId;
    evt.AttackerUnitId = UnitLookup.GetUnitId(ref frame, attacker);
    evt.TargetUnitId = targetUnitId;
    evt.WindupSeconds = FP64.FromInt(windupTicks * TickMath.DeltaTimeMs(ref frame)) / FP64.FromInt(1000);
    evt.AttackerPosition = PositionOf(ref frame, attacker);
    evt.TargetPosition = PositionOf(ref frame, target);
    frame.EventRaiser.RaiseEvent(evt);
  }

  public static void RaiseWindupCanceled(ref Frame frame, EntityRef attacker, int targetUnitId,
    int attackHitId) {
    if (frame.EventRaiser == null)
      return;

    var evt = EventPool.Get<AttackWindupCanceledEvent>();
    evt.AttackHitId = attackHitId;
    evt.AttackerUnitId = UnitLookup.GetUnitId(ref frame, attacker);
    evt.TargetUnitId = targetUnitId;
    frame.EventRaiser.RaiseEvent(evt);
  }

  private static FPVector3 PositionOf(ref Frame frame, EntityRef entity) {
    return frame.Has<TransformComponent>(entity)
      ? frame.GetReadOnly<TransformComponent>(entity).Position
      : default;
  }
}
