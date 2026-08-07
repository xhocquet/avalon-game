using System.Collections.Generic;
using Meesles.Avalon.Sim;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Commands;
using Meesles.Avalon.Sim.Components;
using Meesles.Avalon.Sim.Heroes;
using Meesles.Avalon.Sim.Navigation;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.Deterministic.Navigation;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Logging;
using MoveCommand = Meesles.Avalon.Sim.Commands.MoveCommand;

namespace Meesles.Avalon;

public class CommandSystem(NavigationRuntime navigation = null) : ISystem, ICommandSystem {
  private readonly List<FPVector3> _formationDestinations = [];
  private readonly List<FormationUnit> _formationUnits = [];
  private readonly bool _moveNavAgentsDirectly = navigation == null;
  private readonly UnitLookup.Index _unitIndex = new();

  public void OnCommand(ref Frame frame, ICommand command) {
    if (!CommandValidation.Accept(ref frame, command))
      return;

    switch (command) {
      case MoveCommand move:
        HandleMoveCommand(ref frame, move);
        break;
      case AttackCommand attack:
        HandleAttackCommand(ref frame, attack);
        break;
      case SelectFactionCommand faction:
        FactionActions.TrySelect(ref frame, faction.PlayerId, faction.FactionId);
        break;
      case PurchaseItemCommand purchase:
        ShopActions.TryPurchase(ref frame, purchase.PlayerId, purchase.ItemAssetId);
        break;
      case UpgradeSkillCommand upgrade:
        SkillActions.TryUpgrade(ref frame, upgrade.PlayerId, upgrade.Slot);
        break;
      case CastSkillCommand cast:
        SkillActions.TryCast(ref frame, cast.PlayerId, cast.Slot,
          new FPVector3(cast.TargetX, FP64.Zero, cast.TargetZ));
        break;
    }
  }

  public void Update(ref Frame frame) {
    var rules = frame.AssetRegistry.Get<MovementRulesAsset>();

    var dt = FP64.FromInt(frame.DeltaTimeMs) / FP64.FromInt(1000);

    // StatsComponent is in the filter because it carries the speed: a unit with no stat block has no speed
    // to move at, and every unit that can be ordered around (hero, minion) has one.
    var filter = frame.Filter<UnitMoveTarget, TransformComponent, StatsComponent>();
    while (filter.Next(out var entity)) {
      if (!_moveNavAgentsDirectly && frame.Has<NavAgentComponent>(entity))
        continue;

      ref var moveTarget = ref frame.Get<UnitMoveTarget>(entity);
      ref var transform = ref frame.Get<TransformComponent>(entity);
      var step = frame.GetReadOnly<StatsComponent>(entity).MoveSpeed * dt;
      var toTarget = moveTarget.Target - transform.Position;

      toTarget.y = FP64.Zero;
      var dist = toTarget.magnitude;
      if (dist <= rules.StopDistance) {
        frame.Remove<UnitMoveTarget>(entity);
        continue;
      }

      var move = toTarget.normalized * step;
      if (step >= dist) move = toTarget;
      transform.Position += move;
      transform.Rotation = FP64.Atan2(move.x, move.z);
    }
  }

  private void HandleMoveCommand(ref Frame frame, MoveCommand command) {
    var target = new FPVector3(command.TargetX, FP64.Zero, command.TargetZ);
    if (command.UnitIds.Count > 0) {
      ApplySelectedUnitTargets(ref frame, command, target);
      return;
    }

    ApplyLocalHeroTarget(ref frame, command.PlayerId, target);
  }

  private void HandleAttackCommand(ref Frame frame, AttackCommand command) {
    var unitIndex = RebuildUnitIndex(ref frame);
    if (!CollectOrderedUnits(ref frame, unitIndex, command.PlayerId, command.UnitIds, _formationUnits,
          out var playerTeamId))
      return;

    if (!TryResolveAttackTarget(ref frame, unitIndex, command, playerTeamId, out var targetEntity))
      return;

    ref readonly var targetTransform = ref frame.GetReadOnly<TransformComponent>(targetEntity);
    for (var i = 0; i < _formationUnits.Count; i++) {
      var source = _formationUnits[i];
      UnitIntent.SetMoveTarget(ref frame, source.Entity, targetTransform.Position);
      UnitIntent.SetAttackTarget(ref frame, source.Entity, command.TargetUnitId);
      frame.Logger.KDebug(
        $"[Combat] AttackCommand accepted tick={frame.Tick} playerId={command.PlayerId} sourceUnitId={source.UnitId} targetUnitId={command.TargetUnitId} moveTarget=({targetTransform.Position.x}, {targetTransform.Position.z})");
    }
  }

  // Beyond the shared hostility rule an attack order also needs the target's position, to seed the
  // ordered units' move target. Team-id overload: the issuing player is not itself an entity here.
  private static bool TryResolveAttackTarget(ref Frame frame, UnitLookup.Index unitIndex,
    AttackCommand command, int playerTeamId, out EntityRef targetEntity) {
    return unitIndex.TryGet(command.TargetUnitId, out targetEntity) &&
           frame.Has<TransformComponent>(targetEntity) &&
           CombatTargeting.IsHostileAndAlive(ref frame, playerTeamId, targetEntity);
  }

  private void ApplySelectedUnitTargets(ref Frame frame, MoveCommand command, FPVector3 target) {
    var unitIndex = RebuildUnitIndex(ref frame);
    if (!CollectOrderedUnits(ref frame, unitIndex, command.PlayerId, command.UnitIds, _formationUnits, out _))
      return;

    var rules = frame.AssetRegistry.Get<MovementRulesAsset>();
    if (_formationUnits.Count == 1 || rules == null) {
      for (var i = 0; i < _formationUnits.Count; i++)
        SetTarget(ref frame, _formationUnits[i].Entity, target);
      return;
    }

    GroupFormation.Solve(_formationUnits, target, rules, navigation?.Query, _formationDestinations);
    for (var i = 0; i < _formationUnits.Count; i++)
      SetTarget(ref frame, _formationUnits[i].Entity, _formationDestinations[i]);
  }

  // The field is storage, not state: Rebuild clears and refills the same dictionary, so repeat orders
  // cost no allocation, while the contents never outlive the command that asked for them. An index
  // held across ticks would survive a rollback and resolve ids against a frame that no longer exists.
  private UnitLookup.Index RebuildUnitIndex(ref Frame frame) {
    _unitIndex.Rebuild(ref frame);
    return _unitIndex;
  }

  // Shared front half of every unit order: resolve the commanded ids to entities the issuing player
  // actually controls. False means the order has nobody to act on and should be dropped.
  private static bool CollectOrderedUnits(ref Frame frame, UnitLookup.Index unitIndex, int playerId,
    UnitIdList unitIds, List<FormationUnit> units, out int teamId) {
    units.Clear();
    if (!UnitLookup.TryGetPlayerTeamId(ref frame, playerId, out teamId))
      return false;

    for (var i = 0; i < unitIds.Count; i++) {
      if (!unitIndex.TryGetControllableTeamUnitById(ref frame, teamId, unitIds[i], out var entity))
        continue;

      ref readonly var unit = ref frame.GetReadOnly<UnitIdComponent>(entity);
      ref readonly var transform = ref frame.GetReadOnly<TransformComponent>(entity);
      units.Add(new FormationUnit(entity, unit.UnitId, frame.Has<Hero>(entity), transform.Position));
    }

    return units.Count > 0;
  }

  private static void ApplyLocalHeroTarget(ref Frame frame, int playerId, FPVector3 target) {
    if (UnitLookup.TryGetPlayerHero(ref frame, playerId, out var hero))
      SetTarget(ref frame, hero, target);
  }

  // A move order cancels any standing attack order.
  private static void SetTarget(ref Frame frame, EntityRef entity, FPVector3 target) {
    UnitIntent.ClearAttackIntent(ref frame, entity);
    UnitIntent.SetMoveTarget(ref frame, entity, target);
  }
}
