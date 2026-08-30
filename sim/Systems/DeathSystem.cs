using System.Collections.Generic;
using Meesles.Avalon.Sim;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon;

public class DeathSystem : ISystem {
  private readonly List<DeadUnitSnapshot> _deadUnits = [];

  // Rebuilt from scratch on the first death of each tick and never read before that rebuild, so
  // nothing here survives a rollback.
  private readonly UnitLookup.Index _unitIdIndex = new();

  public void Update(ref Frame frame) {
    _deadUnits.Clear();
    var indexBuilt = false;

    // Units that come back die through RespawnSystem, not here.
    var filter = frame.FilterWithout<UnitIdentity, Health, Respawns>();
    while (filter.Next(out var entity)) {
      ref readonly var health = ref frame.GetReadOnly<Health>(entity);
      if (health.IsAlive)
        continue;

      var lastDamagerUnitId = health.LastDamagerUnitId;
      if (!indexBuilt) {
        _unitIdIndex.Rebuild(ref frame);
        indexBuilt = true;
      }

      ref readonly var unit = ref frame.GetReadOnly<UnitIdentity>(entity);
      var destroyed = ResolveUnitContext(ref frame, entity);
      var destroyer = ResolveDestroyerContext(ref frame, lastDamagerUnitId);
      var position = FPVector3.Zero;
      if (frame.Has<TransformComponent>(entity))
        position = frame.GetReadOnly<TransformComponent>(entity).Position;

      _deadUnits.Add(new DeadUnitSnapshot(
        entity,
        unit.UnitId,
        unit.UnitTypeId,
        destroyed.TeamId,
        destroyer.Entity,
        destroyer.UnitId,
        destroyer.UnitTypeId,
        destroyer.TeamId,
        position,
        frame.Has<Crystal>(entity),
        frame.Has<Crystal>(entity) ? frame.GetReadOnly<Crystal>(entity).CrystalId : 0,
        frame.Has<Turret>(entity)));
    }

    // Paid out before anything is destroyed: a killer may itself be on the dead list this tick, and
    // it still earns what it killed.
    foreach (var dead in _deadUnits) {
      ExperienceRewards.AwardForKill(ref frame, dead.DestroyerEntity, dead.UnitTypeId, dead.TeamId);
      GoldRewards.AwardForKill(ref frame, dead.DestroyerEntity, dead.UnitTypeId, dead.TeamId);
      MatchStats.RecordKill(ref frame, dead.DestroyerEntity, dead.UnitTypeId, dead.TeamId);
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
      evt.DestroyerUnitId = dead.DestroyerUnitId;
      evt.DestroyerTeamId = dead.DestroyerTeamId;
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
    unitDied.DestroyerUnitId = dead.DestroyerUnitId;
    unitDied.DestroyerUnitTypeId = dead.DestroyerUnitTypeId;
    unitDied.Position = dead.Position;
    frame.EventRaiser.RaiseEvent(unitDied);
  }

  // The killer is whoever landed the fatal hit, recorded by DamageSystem. It may already be dead
  // itself this tick, but every destroy happens after the snapshot pass, so it still resolves.
  private UnitContext ResolveDestroyerContext(ref Frame frame, int lastDamagerUnitId) {
    if (lastDamagerUnitId == 0 || !_unitIdIndex.TryGet(lastDamagerUnitId, out var destroyer))
      return default;

    return ResolveUnitContext(ref frame, destroyer);
  }

  private static UnitContext ResolveUnitContext(ref Frame frame, EntityRef entity) {
    var teamId = frame.Has<Team>(entity) ? frame.GetReadOnly<Team>(entity).TeamId : 0;
    return new UnitContext(
      entity,
      UnitLookup.GetUnitId(ref frame, entity),
      UnitLookup.GetUnitTypeId(ref frame, entity),
      teamId);
  }

  private readonly struct DeadUnitSnapshot(
    EntityRef entity,
    int unitId,
    int unitTypeId,
    int teamId,
    EntityRef destroyerEntity,
    int destroyerUnitId,
    int destroyerUnitTypeId,
    int destroyerTeamId,
    FPVector3 position,
    bool isCrystal,
    int crystalId,
    bool isTurret) {
    public readonly EntityRef Entity = entity;
    public readonly int UnitId = unitId;
    public readonly int UnitTypeId = unitTypeId;
    public readonly int TeamId = teamId;

    // Only valid for the rest of this tick - the killer may be destroyed by the pass below.
    public readonly EntityRef DestroyerEntity = destroyerEntity;
    public readonly int DestroyerUnitId = destroyerUnitId;
    public readonly int DestroyerUnitTypeId = destroyerUnitTypeId;
    public readonly int DestroyerTeamId = destroyerTeamId;
    public readonly FPVector3 Position = position;
    public readonly bool IsCrystal = isCrystal;
    public readonly int CrystalId = crystalId;
    public readonly bool IsTurret = isTurret;
  }

  private readonly struct UnitContext(EntityRef entity, int unitId, int unitTypeId, int teamId) {
    public readonly EntityRef Entity = entity;
    public readonly int UnitId = unitId;
    public readonly int UnitTypeId = unitTypeId;
    public readonly int TeamId = teamId;
  }
}
