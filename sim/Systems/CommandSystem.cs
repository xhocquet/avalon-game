using System.Collections.Generic;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Commands;
using Meesles.Avalon.Sim.Components;
using Meesles.Avalon.Sim.Navigation;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.Deterministic.Navigation;
using xpTURN.Klotho.ECS;
using MoveCommand = Meesles.Avalon.Sim.Commands.MoveCommand;

namespace Meesles.Avalon.Sim;

public class CommandSystem(NavigationRuntime navigation = null) : ISystem, ICommandSystem {
  private readonly List<EntityRef> _arrived = [];
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
        FactionController.TrySelect(ref frame, faction.PlayerId, faction.FactionId);
        break;
      case PurchaseItemCommand purchase:
        ShopController.TryPurchase(ref frame, purchase.PlayerId, purchase.ItemAssetId);
        break;
      case UpgradeSkillCommand upgrade:
        SkillsController.TryUpgrade(ref frame, upgrade.PlayerId, upgrade.Slot);
        break;
      case CastSkillCommand cast:
        SkillsController.TryCast(ref frame, cast.PlayerId, cast.Slot,
          new FPVector3(cast.TargetX, FP64.Zero, cast.TargetZ));
        break;
      case SetCheatCommand cheat:
        CheatsController.Set(ref frame, cheat.PlayerId, (CheatFlags)cheat.Flags, cheat.Enabled != 0);
        break;
      case DebugCommand debug:
        DebugController.Execute(ref frame, debug.PlayerId, (DebugAction)debug.Action, debug.Param,
          debug.FactionId, new FPVector3(debug.TargetX, FP64.Zero, debug.TargetZ));
        break;
    }
  }

  public void Update(ref Frame frame) {
    var rules = frame.AssetRegistry.Get<MovementRulesAsset>();

    var dt = FP64.FromInt(frame.DeltaTimeMs) / FP64.FromInt(1000);
    _arrived.Clear();

    // Stats supplies movement speed.
    var filter = frame.Filter<UnitMoveTarget, TransformComponent, Stats>();
    while (filter.Next(out var entity)) {
      if (!_moveNavAgentsDirectly && frame.Has<NavAgentComponent>(entity))
        continue;

      // Keep the order while snared.
      if (SnareController.IsSnared(ref frame, entity))
        continue;

      ref var moveTarget = ref frame.Get<UnitMoveTarget>(entity);
      ref var transform = ref frame.Get<TransformComponent>(entity);
      var step = frame.GetReadOnly<Stats>(entity).MoveSpeed * dt;
      if (Planar.MoveTowards(ref transform, moveTarget.Target, step, rules.StopDistance)) {
        _arrived.Add(entity);
        continue;
      }
    }

    // Deferred: UnitMoveTarget is one of the filter's own types (see the iteration rule in AGENTS.md).
    for (var i = 0; i < _arrived.Count; i++)
      frame.Remove<UnitMoveTarget>(_arrived[i]);
  }

  private void HandleMoveCommand(ref Frame frame, MoveCommand command) {
    var target = ResolveMoveTarget(ref frame, new FPVector3(command.TargetX, FP64.Zero, command.TargetZ));
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
    var approach = NavTargets.SnapToWalkable(navigation?.Query, targetTransform.Position);
    for (var i = 0; i < _formationUnits.Count; i++) {
      var source = _formationUnits[i];
      UnitIntentController.SetMoveTarget(ref frame, source.Entity, approach);
      UnitIntentController.AllowImmediateRepath(ref frame, source.Entity);
      UnitIntentController.SetAttackTarget(ref frame, source.Entity, command.TargetUnitId);
      SimLog.Debug(ref frame,
        $"[Command] event=attack_order_accepted tick={frame.Tick} playerId={command.PlayerId} sourceUnitId={source.UnitId} targetUnitId={command.TargetUnitId} moveTarget=({approach.x},{approach.z})");
    }
  }

  // Attack orders also need a target position for their approach target.
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

    GroupFormation.Solve(_formationUnits, target, rules, navigation?.NavMesh, navigation?.Query,
      _formationDestinations);
    for (var i = 0; i < _formationUnits.Count; i++)
      SetTarget(ref frame, _formationUnits[i].Entity, _formationDestinations[i]);
  }

  // Rebuild per command: this cached index must not survive a rollback.
  private UnitLookup.Index RebuildUnitIndex(ref Frame frame) {
    _unitIndex.Rebuild(ref frame);
    return _unitIndex;
  }

  // Resolves controlled units from the command payload.
  private static bool CollectOrderedUnits(ref Frame frame, UnitLookup.Index unitIndex, int playerId,
    UnitIdList unitIds, List<FormationUnit> units, out int teamId) {
    units.Clear();
    if (!UnitLookup.TryGetPlayerTeamId(ref frame, playerId, out teamId))
      return false;

    for (var i = 0; i < unitIds.Count; i++) {
      if (!unitIndex.TryGetControllableTeamUnitById(ref frame, teamId, unitIds[i], out var entity))
        continue;

      ref readonly var unit = ref frame.GetReadOnly<UnitIdentity>(entity);
      ref readonly var transform = ref frame.GetReadOnly<TransformComponent>(entity);
      units.Add(new FormationUnit(entity, unit.UnitId, frame.Has<Hero>(entity), transform.Position));
    }

    return units.Count > 0;
  }

  private static void ApplyLocalHeroTarget(ref Frame frame, int playerId, FPVector3 target) {
    if (UnitLookup.TryGetPlayerHero(ref frame, playerId, out var hero))
      SetTarget(ref frame, hero, target);
  }

  // Re-resolve raw commands with the same target rules as the client.
  private FPVector3 ResolveMoveTarget(ref Frame frame, FPVector3 target) {
    if (navigation == null)
      return target;

    var rules = frame.AssetRegistry.Get<MovementRulesAsset>();
    var clearance = rules != null ? rules.MoveTargetEdgeClearance : FP64.Zero;
    return NavTargets.ResolveMoveTarget(navigation.NavMesh, navigation.Query, target, clearance);
  }

  // A move order cancels any standing attack order.
  private static void SetTarget(ref Frame frame, EntityRef entity, FPVector3 target) {
    UnitIntentController.ClearAttackIntent(ref frame, entity);
    UnitIntentController.SetMoveTarget(ref frame, entity, target);
    UnitIntentController.AllowImmediateRepath(ref frame, entity);
  }
}
