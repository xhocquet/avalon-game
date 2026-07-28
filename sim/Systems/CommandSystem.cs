using System.Collections.Generic;
using Meesles.Avalon.Sim;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Commands;
using Meesles.Avalon.Sim.Components;
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
    switch (command) {
      case MoveCommand move:
        HandleMoveCommand(ref frame, move);
        break;
      case AttackCommand attack:
        HandleAttackCommand(ref frame, attack);
        break;
      case SelectFactionCommand faction:
        HandleSelectFactionCommand(ref frame, faction);
        break;
      case PurchaseItemCommand purchase:
        HandlePurchaseItemCommand(ref frame, purchase);
        break;
    }
  }

  public void Update(ref Frame frame) {
    var stats = frame.AssetRegistry.Get<PlayerStatsAsset>();
    var rules = frame.AssetRegistry.Get<MovementRulesAsset>();

    var dt = FP64.FromInt(frame.DeltaTimeMs) / FP64.FromInt(1000);
    var step = stats.MoveSpeed * dt;

    var filter = frame.Filter<UnitMoveTarget, TransformComponent>();
    while (filter.Next(out var entity)) {
      if (!_moveNavAgentsDirectly && frame.Has<NavAgentComponent>(entity))
        continue;

      ref var moveTarget = ref frame.Get<UnitMoveTarget>(entity);
      ref var transform = ref frame.Get<TransformComponent>(entity);

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

  private static void HandleSelectFactionCommand(ref Frame frame, SelectFactionCommand command) {
    var filter = frame.Filter<PlayerFaction>();
    while (filter.Next(out var entity)) {
      ref var slot = ref frame.Get<PlayerFaction>(entity);
      if (slot.PlayerId != command.PlayerId)
        continue;

      slot.FactionId = command.FactionId;
      slot.Confirmed = 1;
      return;
    }
  }

  private static void HandlePurchaseItemCommand(ref Frame frame, PurchaseItemCommand command) {
    if (!TryGetPlayerHero(ref frame, command.PlayerId, out var heroEntity)) {
      frame.Logger.KInformation(
        $"[Shop] REJECT tick={frame.Tick} playerId={command.PlayerId} reason=no_hero_for_player");
      return;
    }

    if (!frame.AssetRegistry.TryGet<ShopItemAsset>(command.ItemAssetId, out var item)) {
      frame.Logger.KInformation(
        $"[Shop] REJECT tick={frame.Tick} playerId={command.PlayerId} itemId={command.ItemAssetId} reason=item_asset_missing");
      return;
    }

    if (!frame.Has<Inventory>(heroEntity) || !frame.Has<Stats>(heroEntity)) {
      frame.Logger.KInformation(
        $"[Shop] REJECT tick={frame.Tick} playerId={command.PlayerId} reason=hero_missing_inventory_or_stats hasInv={frame.Has<Inventory>(heroEntity)} hasStats={frame.Has<Stats>(heroEntity)}");
      return;
    }

    ref var inventory = ref frame.Get<Inventory>(heroEntity);
    if (inventory.Gold < item.Cost) {
      frame.Logger.KInformation(
        $"[Shop] REJECT tick={frame.Tick} playerId={command.PlayerId} itemId={command.ItemAssetId} reason=insufficient_gold gold={inventory.Gold} cost={item.Cost}");
      return;
    }

    if (!IsHeroNearTeamShop(ref frame, heroEntity)) {
      frame.Logger.KInformation(
        $"[Shop] REJECT tick={frame.Tick} playerId={command.PlayerId} itemId={command.ItemAssetId} reason=out_of_range");
      return;
    }

    if (inventory.IsItemsFull) {
      frame.Logger.KInformation(
        $"[Shop] REJECT tick={frame.Tick} playerId={command.PlayerId} itemId={command.ItemAssetId} reason=inventory_full itemCount={inventory.ItemCount}");
      return;
    }

    inventory.Gold -= item.Cost;
    inventory.TryAddItem(command.ItemAssetId);
    ref var stats = ref frame.Get<Stats>(heroEntity);
    stats.Add(StatType.Strength, item.AttackBonus);

    frame.Logger.KInformation(
      $"[Shop] ACCEPT tick={frame.Tick} playerId={command.PlayerId} itemId={command.ItemAssetId} cost={item.Cost} +str={item.AttackBonus} goldLeft={inventory.Gold} strengthNow={stats.Strength} items={inventory.ItemCount}");
  }

  private static bool TryGetPlayerHero(ref Frame frame, int playerId, out EntityRef heroEntity) {
    var filter = frame.Filter<Hero>();
    while (filter.Next(out var entity)) {
      ref readonly var hero = ref frame.GetReadOnly<Hero>(entity);
      if (hero.PlayerId != playerId)
        continue;

      heroEntity = entity;
      return true;
    }

    heroEntity = default;
    return false;
  }

  private static bool IsHeroNearTeamShop(ref Frame frame, EntityRef heroEntity) {
    if (!frame.Has<Team>(heroEntity) || !frame.Has<TransformComponent>(heroEntity))
      return false;

    var teamId = frame.GetReadOnly<Team>(heroEntity).TeamId;
    if (!frame.AssetRegistry.TryGet<MapLayoutAsset>(out var layout))
      return false;

    if (!layout.TryGetByTypeAndTeam(MapMarkerType.Shop, teamId, out var shopPos))
      return false;

    if (!frame.AssetRegistry.TryGet<ShopRulesAsset>(out var shopRules))
      return false;

    var heroPos = frame.GetReadOnly<TransformComponent>(heroEntity).Position;
    var delta = heroPos - shopPos;
    delta.y = FP64.Zero;

    var range = shopRules.InteractRange;
    return delta.sqrMagnitude <= range * range;
  }

  private void HandleMoveCommand(ref Frame frame, MoveCommand command) {
    var target = new FPVector3(command.TargetX, FP64.Zero, command.TargetZ);
    if (command.UnitIdCount > 0) {
      ApplySelectedUnitTargets(ref frame, command, target);
      return;
    }

    ApplyLocalHeroTarget(ref frame, command.PlayerId, target);
  }

  private void HandleAttackCommand(ref Frame frame, AttackCommand command) {
    _unitIndex.Rebuild(ref frame);
    if (!TryResolveAttackTarget(ref frame, command, out var targetEntity, out var playerTeamId))
      return;

    ref readonly var targetTransform = ref frame.GetReadOnly<TransformComponent>(targetEntity);
    for (var i = 0; i < command.SourceUnitIdCount; i++) {
      var sourceUnitId = command.GetSourceUnitId(i);
      if (!_unitIndex.TryGetControllableTeamUnitById(ref frame, playerTeamId, sourceUnitId, out var sourceEntity))
        continue;

      SetAttackMoveTarget(ref frame, sourceEntity, targetTransform.Position);
      SetAttackTarget(ref frame, sourceEntity, command.TargetUnitId);
      frame.Logger.KDebug(
        $"[Combat] AttackCommand accepted tick={frame.Tick} playerId={command.PlayerId} sourceUnitId={sourceUnitId} targetUnitId={command.TargetUnitId} moveTarget=({targetTransform.Position.x}, {targetTransform.Position.z})");
    }
  }

  private bool TryResolveAttackTarget(ref Frame frame, AttackCommand command,
    out EntityRef targetEntity, out int playerTeamId) {
    playerTeamId = 0;
    if (!_unitIndex.TryGet(command.TargetUnitId, out targetEntity))
      return false;

    if (!frame.Has<Team>(targetEntity) || !frame.Has<Health>(targetEntity) ||
        !frame.Has<TransformComponent>(targetEntity))
      return false;

    ref readonly var health = ref frame.GetReadOnly<Health>(targetEntity);
    if (health.Current <= 0)
      return false;

    if (!UnitLookup.TryGetPlayerTeamId(ref frame, command.PlayerId, out playerTeamId))
      return false;

    ref readonly var targetTeam = ref frame.GetReadOnly<Team>(targetEntity);
    if (targetTeam.TeamId == playerTeamId)
      return false;

    return true;
  }

  private static void SetAttackMoveTarget(ref Frame frame, EntityRef entity, FPVector3 target) {
    target.y = FP64.Zero;
    UnitIntent.SetMoveTarget(ref frame, entity, target);
  }

  private static void SetAttackTarget(ref Frame frame, EntityRef entity, int targetUnitId) {
    if (frame.Has<AttackTargetUnitId>(entity)) {
      ref var attackTarget = ref frame.Get<AttackTargetUnitId>(entity);
      attackTarget.TargetUnitId = targetUnitId;
      return;
    }

    frame.Add(entity, new AttackTargetUnitId { TargetUnitId = targetUnitId });
  }

  private void ApplySelectedUnitTargets(ref Frame frame, MoveCommand command, FPVector3 target) {
    CollectSelectedUnits(ref frame, command, _formationUnits);
    if (_formationUnits.Count == 0)
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

  private void CollectSelectedUnits(ref Frame frame, MoveCommand command, List<FormationUnit> units) {
    units.Clear();
    if (!UnitLookup.TryGetPlayerTeamId(ref frame, command.PlayerId, out var teamId))
      return;

    _unitIndex.Rebuild(ref frame);
    for (var i = 0; i < command.UnitIdCount; i++) {
      var unitId = command.GetUnitId(i);
      if (!_unitIndex.TryGetControllableTeamUnitById(ref frame, teamId, unitId, out var entity))
        continue;

      ref readonly var unit = ref frame.GetReadOnly<Unit>(entity);
      ref readonly var transform = ref frame.GetReadOnly<TransformComponent>(entity);
      units.Add(new FormationUnit(entity, unit.UnitId, frame.Has<Hero>(entity), transform.Position));
    }
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

  // A move order cancels any standing attack order.
  private static void SetTarget(ref Frame frame, EntityRef entity, FPVector3 target) {
    UnitIntent.ClearAttackIntent(ref frame, entity);
    UnitIntent.SetMoveTarget(ref frame, entity, target);
  }
}
