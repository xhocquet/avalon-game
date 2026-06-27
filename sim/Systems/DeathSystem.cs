using System.Collections.Generic;
using Meesles.Avalon.Sim;
using Meesles.Avalon.Sim.Models;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon {
  public class DeathSystem : ISystem {
    private readonly List<DeadUnitSnapshot> _deadUnits = new();

    public void Update(ref Frame frame) {
      _deadUnits.Clear();

      var filter = frame.Filter<Unit, Health>();
      while (filter.Next(out var entity)) {
        if (frame.Has<Player>(entity))
          continue;

        ref readonly var health = ref frame.GetReadOnly<Health>(entity);
        if (health.Current > 0)
          continue;

        ref readonly var unit = ref frame.GetReadOnly<Unit>(entity);
        FPVector3 position = FPVector3.Zero;
        if (frame.Has<TransformComponent>(entity))
          position = frame.GetReadOnly<TransformComponent>(entity).Position;

        _deadUnits.Add(new DeadUnitSnapshot(entity, unit.UnitId, unit.UnitTypeId, position));
      }

      for (int i = 0; i < _deadUnits.Count; i++) {
        DeadUnitSnapshot dead = _deadUnits[i];

        if (frame.EventRaiser != null) {
          var evt = EventPool.Get<UnitDiedEvent>();
          evt.UnitId = dead.UnitId;
          evt.UnitTypeId = dead.UnitTypeId;
          evt.Position = dead.Position;
          frame.EventRaiser.RaiseEvent(evt);
        }

        frame.DestroyEntity(dead.Entity);
      }
    }

    private readonly struct DeadUnitSnapshot {
      public readonly EntityRef Entity;
      public readonly int UnitId;
      public readonly int UnitTypeId;
      public readonly FPVector3 Position;

      public DeadUnitSnapshot(EntityRef entity, int unitId, int unitTypeId, FPVector3 position) {
        Entity = entity;
        UnitId = unitId;
        UnitTypeId = unitTypeId;
        Position = position;
      }
    }
  }
}
