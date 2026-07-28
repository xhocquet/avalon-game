using System.Collections.Generic;
using Meesles.Avalon.Sim;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon;

public class DeathSystem : ISystem {
  private readonly List<DeadUnitSnapshot> _deadUnits = [];

  public void Update(ref Frame frame) {
    _deadUnits.Clear();

    // Players die through RespawnSystem, not here.
    var filter = frame.FilterWithout<Unit, Health, Player>();
    while (filter.Next(out var entity)) {
      ref readonly var health = ref frame.GetReadOnly<Health>(entity);
      if (health.Current > 0)
        continue;

      ref readonly var unit = ref frame.GetReadOnly<Unit>(entity);
      var destroyed = ResolveUnitContext(ref frame, entity);
      var destroyer = ResolveDestroyerContext(ref frame, entity);
      var position = FPVector3.Zero;
      if (frame.Has<TransformComponent>(entity))
        position = frame.GetReadOnly<TransformComponent>(entity).Position;

      _deadUnits.Add(new DeadUnitSnapshot(
        entity,
        unit.UnitId,
        unit.UnitTypeId,
        destroyed.TeamId,
        destroyed.OwnerId,
        destroyer.UnitId,
        destroyer.TeamId,
        destroyer.OwnerId,
        position,
        frame.Has<Crystal>(entity),
        frame.Has<Crystal>(entity) ? frame.GetReadOnly<Crystal>(entity).CrystalId : 0,
        frame.Has<Turret>(entity)));
    }

    foreach (var dead in _deadUnits) {
      RaiseDeathEvent(ref frame, dead);
      frame.DestroyEntity(dead.Entity);
    }
  }

  private static void RaiseDeathEvent(ref Frame frame, DeadUnitSnapshot dead) {
    if (frame.EventRaiser == null)
      return;

    if (dead.IsCrystal) {
      var evt = EventPool.Get<CrystalDestroyedEvent>();
      evt.UnitId = dead.UnitId;
      evt.CrystalId = dead.CrystalId;
      evt.TeamId = dead.TeamId;
      evt.OwnerId = dead.OwnerId;
      evt.DestroyerUnitId = dead.DestroyerUnitId;
      evt.DestroyerTeamId = dead.DestroyerTeamId;
      evt.DestroyerOwnerId = dead.DestroyerOwnerId;
      evt.Position = dead.Position;
      frame.EventRaiser.RaiseEvent(evt);
      return;
    }

    if (dead.IsTurret) {
      var evt = EventPool.Get<TurretDestroyedEvent>();
      evt.UnitId = dead.UnitId;
      evt.DestroyerUnitId = dead.DestroyerUnitId;
      evt.Position = dead.Position;
      frame.EventRaiser.RaiseEvent(evt);
      return;
    }

    var unitDied = EventPool.Get<UnitDiedEvent>();
    unitDied.UnitId = dead.UnitId;
    unitDied.UnitTypeId = dead.UnitTypeId;
    unitDied.Position = dead.Position;
    frame.EventRaiser.RaiseEvent(unitDied);
  }

  private static UnitContext ResolveDestroyerContext(ref Frame frame, EntityRef deadEntity) {
    UnitContext destroyer = default;
    var filter = frame.Filter<Unit, Combat>();
    while (filter.Next(out var attacker)) {
      ref readonly var combat = ref frame.GetReadOnly<Combat>(attacker);
      if (combat.Target != deadEntity)
        continue;

      var candidate = ResolveUnitContext(ref frame, attacker);
      if (destroyer.UnitId == 0 || candidate.UnitId < destroyer.UnitId)
        destroyer = candidate;
    }

    return destroyer;
  }

  private static UnitContext ResolveUnitContext(ref Frame frame, EntityRef entity) {
    var unitId = frame.Has<Unit>(entity) ? frame.GetReadOnly<Unit>(entity).UnitId : 0;
    var teamId = frame.Has<Team>(entity) ? frame.GetReadOnly<Team>(entity).TeamId : 0;
    var ownerId = frame.Has<OwnerComponent>(entity) ? frame.GetReadOnly<OwnerComponent>(entity).OwnerId : 0;
    return new UnitContext(unitId, teamId, ownerId);
  }

  private readonly struct DeadUnitSnapshot(
    EntityRef entity,
    int unitId,
    int unitTypeId,
    int teamId,
    int ownerId,
    int destroyerUnitId,
    int destroyerTeamId,
    int destroyerOwnerId,
    FPVector3 position,
    bool isCrystal,
    int crystalId,
    bool isTurret) {
    public readonly EntityRef Entity = entity;
    public readonly int UnitId = unitId;
    public readonly int UnitTypeId = unitTypeId;
    public readonly int TeamId = teamId;
    public readonly int OwnerId = ownerId;
    public readonly int DestroyerUnitId = destroyerUnitId;
    public readonly int DestroyerTeamId = destroyerTeamId;
    public readonly int DestroyerOwnerId = destroyerOwnerId;
    public readonly FPVector3 Position = position;
    public readonly bool IsCrystal = isCrystal;
    public readonly int CrystalId = crystalId;
    public readonly bool IsTurret = isTurret;
  }

  private readonly struct UnitContext(int unitId, int teamId, int ownerId) {
    public readonly int UnitId = unitId;
    public readonly int TeamId = teamId;
    public readonly int OwnerId = ownerId;
  }
}
