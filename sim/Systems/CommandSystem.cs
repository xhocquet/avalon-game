using System.Collections.Generic;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim;
using Meesles.Avalon.Sim.Models;

namespace Meesles.Avalon {
  public class CommandSystem : ISystem, ICommandSystem {
    private static readonly FP64 StopDistance = FP64.FromDouble(0.15);
    private static readonly FP64 FrontFormationSpacing = FP64.FromDouble(1.4);
    private static readonly FP64 TrailFormationSpacing = FP64.FromDouble(2.0);
    private readonly bool _moveNavAgentsDirectly;

    public CommandSystem(bool moveNavAgentsDirectly = true) {
      _moveNavAgentsDirectly = moveNavAgentsDirectly;
    }

    public void OnCommand(ref Frame frame, ICommand command) {
      switch (command) {
        case Sim.Commands.MoveCommand move:
          HandleMoveCommand(ref frame, move);
          break;
        case Sim.Commands.AttackCommand attack:
          HandleAttackCommand(ref frame, attack);
          break;
      }
    }

    private static void HandleMoveCommand(ref Frame frame, Sim.Commands.MoveCommand command) {
      FPVector3 target = new FPVector3(command.TargetX, FP64.Zero, command.TargetZ);
      if (command.UnitIdCount > 0) {
        ApplySelectedUnitTargets(ref frame, command, target);
        return;
      }

      ApplyLocalHeroTarget(ref frame, command.PlayerId, target);
    }

    private static void HandleAttackCommand(ref Frame frame, Sim.Commands.AttackCommand command) {
      if (!TryResolveAttackTarget(ref frame, command, out var targetEntity))
        return;

      for (int i = 0; i < command.SourceUnitIdCount; i++) {
        int sourceUnitId = command.GetSourceUnitId(i);
        if (!UnitLookup.TryGetPlayerOwnedUnitById(ref frame, command.PlayerId, sourceUnitId, out var sourceEntity))
          continue;

        ClearMoveTarget(ref frame, sourceEntity);
        SetAttackTarget(ref frame, sourceEntity, command.TargetUnitId);
      }
    }

    private static bool TryResolveAttackTarget(ref Frame frame, Sim.Commands.AttackCommand command,
        out EntityRef targetEntity) {
      if (!UnitLookup.TryGetEntityByUnitId(ref frame, command.TargetUnitId, out targetEntity))
        return false;

      if (frame.Has<Health>(targetEntity)) {
        ref readonly var health = ref frame.GetReadOnly<Health>(targetEntity);
        if (health.Current <= 0)
          return false;
      }

      if (!frame.Has<Team>(targetEntity))
        return false;

      if (!UnitLookup.TryGetPlayerTeamId(ref frame, command.PlayerId, out int playerTeamId))
        return false;

      ref readonly var targetTeam = ref frame.GetReadOnly<Team>(targetEntity);
      if (targetTeam.TeamId == playerTeamId)
        return false;

      return true;
    }

    private static void ClearMoveTarget(ref Frame frame, EntityRef entity) {
      if (frame.Has<UnitMoveTarget>(entity))
        frame.Remove<UnitMoveTarget>(entity);
    }

    private static void SetAttackTarget(ref Frame frame, EntityRef entity, int targetUnitId) {
      if (frame.Has<AttackTargetUnitId>(entity)) {
        ref var attackTarget = ref frame.Get<AttackTargetUnitId>(entity);
        attackTarget.TargetUnitId = targetUnitId;
        return;
      }

      frame.Add(entity, new AttackTargetUnitId { TargetUnitId = targetUnitId });
    }

    public void Update(ref Frame frame) {
      var stats = frame.AssetRegistry.Get<PlayerStatsAsset>();
      if (stats == null) return;

      FP64 dt = FP64.FromInt(frame.DeltaTimeMs) / FP64.FromInt(1000);
      FP64 step = stats.MoveSpeed * dt;

      var filter = frame.Filter<UnitMoveTarget, TransformComponent>();
      while (filter.Next(out var entity)) {
        if (!_moveNavAgentsDirectly && frame.Has<xpTURN.Klotho.Deterministic.Navigation.NavAgentComponent>(entity))
          continue;

        ref var moveTarget = ref frame.Get<UnitMoveTarget>(entity);
        ref var transform = ref frame.Get<TransformComponent>(entity);

        FPVector3 toTarget = moveTarget.Target - transform.Position;
        toTarget.y = FP64.Zero;
        FP64 dist = toTarget.magnitude;
        if (dist <= StopDistance) {
          frame.Remove<UnitMoveTarget>(entity);
          continue;
        }

        FPVector3 move = toTarget.normalized * step;
        if (step >= dist) move = toTarget;
        transform.Position = transform.Position + move;
        transform.Rotation = FP64.Atan2(move.x, move.z);
      }
    }

    private static void ApplySelectedUnitTargets(ref Frame frame, Sim.Commands.MoveCommand command, FPVector3 target) {
      var units = GetSelectedUnits(ref frame, command);
      if (units.Count == 0)
        return;

      if (units.Count == 1) {
        SetTarget(ref frame, units[0].Entity, target);
        return;
      }

      ApplyFormationTargets(ref frame, units, target);
    }

    private static List<SelectedUnit> GetSelectedUnits(ref Frame frame, Sim.Commands.MoveCommand command) {
      var units = new List<SelectedUnit>();
      for (int i = 0; i < command.UnitIdCount; i++) {
        int unitId = command.GetUnitId(i);
        if (!UnitLookup.TryGetPlayerOwnedUnitById(ref frame, command.PlayerId, unitId, out var entity))
          continue;

        ref readonly var unit = ref frame.GetReadOnly<Unit>(entity);
        ref readonly var transform = ref frame.GetReadOnly<TransformComponent>(entity);
        units.Add(new SelectedUnit(
          entity,
          unit.UnitId,
          unit.UnitTypeId,
          frame.Has<Hero>(entity),
          transform.Position));
      }

      units.Sort(CompareSelectedUnits);
      return units;
    }

    private static int CompareSelectedUnits(SelectedUnit a, SelectedUnit b) {
      if (a.IsHero != b.IsHero)
        return a.IsHero ? -1 : 1;

      return a.UnitId.CompareTo(b.UnitId);
    }

    private static void ApplyFormationTargets(ref Frame frame, List<SelectedUnit> units, FPVector3 target) {
      FPVector3 centroid = FPVector3.Zero;
      for (int i = 0; i < units.Count; i++)
        centroid += units[i].Position;
      centroid /= FP64.FromInt(units.Count);

      FPVector2 forward = (target - centroid).ToXZ();
      if (forward.sqrMagnitude == FP64.Zero)
        forward = new FPVector2(FP64.Zero, FP64.One);
      else
        forward = forward.normalized;

      FPVector2 right = new FPVector2(forward.y, -forward.x);
      int heroCount = CountHeroes(units);
      int frontCount = heroCount > 0 ? heroCount : 1;

      for (int i = 0; i < units.Count; i++) {
        FPVector3 slot = i < frontCount
          ? GetFrontSlot(target, right, i, frontCount)
          : GetTrailSlot(target, forward, right, i - frontCount);

        SetTarget(ref frame, units[i].Entity, slot);
      }
    }

    private static int CountHeroes(List<SelectedUnit> units) {
      int count = 0;
      for (int i = 0; i < units.Count; i++) {
        if (units[i].IsHero)
          count++;
      }

      return count;
    }

    private static FPVector3 GetFrontSlot(FPVector3 target, FPVector2 right, int index, int count) {
      FP64 lateral = GetCenteredOffset(index, count, FrontFormationSpacing);
      return OffsetTarget(target, right, lateral);
    }

    private static FPVector3 GetTrailSlot(FPVector3 target, FPVector2 forward, FPVector2 right, int index) {
      int row = 1;
      int rowStart = 0;
      while (index >= rowStart + row) {
        rowStart += row;
        row++;
      }

      int slot = index - rowStart;
      FP64 lateral = GetCenteredOffset(slot, row, TrailFormationSpacing);
      FP64 back = TrailFormationSpacing * FP64.FromInt(row);
      FPVector2 offset = right * lateral - forward * back;
      return new FPVector3(target.x + offset.x, target.y, target.z + offset.y);
    }

    private static FP64 GetCenteredOffset(int index, int count, FP64 spacing) {
      return FP64.FromInt(index * 2 - (count - 1)) * spacing * FP64.Half;
    }

    private static FPVector3 OffsetTarget(FPVector3 target, FPVector2 right, FP64 lateral) {
      return new FPVector3(target.x + right.x * lateral, target.y, target.z + right.y * lateral);
    }

    private static void ApplyLocalHeroTarget(ref Frame frame, int playerId, FPVector3 target) {
      var filter = frame.Filter<Player>();
      while (filter.Next(out var entity)) {
        ref readonly var player = ref frame.Get<Player>(entity);
        if (player.PlayerId != playerId) continue;
        SetTarget(ref frame, entity, target);
        return;
      }
    }

    private static void SetTarget(ref Frame frame, EntityRef entity, FPVector3 target) {
      if (frame.Has<AttackTargetUnitId>(entity))
        frame.Remove<AttackTargetUnitId>(entity);

      if (frame.Has<UnitMoveTarget>(entity)) {
        ref var moveTarget = ref frame.Get<UnitMoveTarget>(entity);
        moveTarget.Target = target;
      }
      else {
        frame.Add(entity, new UnitMoveTarget { Target = target });
      }
    }

    private readonly struct SelectedUnit {
      public readonly EntityRef Entity;
      public readonly int UnitId;
      public readonly int UnitTypeId;
      public readonly bool IsHero;
      public readonly FPVector3 Position;

      public SelectedUnit(EntityRef entity, int unitId, int unitTypeId, bool isHero, FPVector3 position) {
        Entity = entity;
        UnitId = unitId;
        UnitTypeId = unitTypeId;
        IsHero = isHero;
        Position = position;
      }
    }
  }
}
