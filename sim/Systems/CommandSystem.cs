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
        HandleSelectFactionCommand(ref frame, faction);
        break;
      case PurchaseItemCommand purchase:
        HandlePurchaseItemCommand(ref frame, purchase);
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

  private static void HandleSelectFactionCommand(ref Frame frame, SelectFactionCommand command) {
    // The pick only feeds HeroSpawnSystem. Once the hero exists it is settled, and a later pick would
    // only re-skin the team's minions in the view layer.
    if (TryGetPlayerHero(ref frame, command.PlayerId, out _)) {
      frame.Logger.KInformation(
        $"[Faction] REJECT tick={frame.Tick} playerId={command.PlayerId} factionId={command.FactionId} reason=hero_already_spawned");
      return;
    }

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

    if (!frame.Has<InventoryComponent>(heroEntity) || !frame.Has<StatsComponent>(heroEntity)) {
      frame.Logger.KInformation(
        $"[Shop] REJECT tick={frame.Tick} playerId={command.PlayerId} reason=hero_missing_inventory_or_stats hasInv={frame.Has<InventoryComponent>(heroEntity)} hasStats={frame.Has<StatsComponent>(heroEntity)}");
      return;
    }

    ref var inventory = ref frame.Get<InventoryComponent>(heroEntity);
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
    ref var stats = ref frame.Get<StatsComponent>(heroEntity);
    stats.Add(StatType.Strength, item.AttackBonus);

    frame.Logger.KInformation(
      $"[Shop] ACCEPT tick={frame.Tick} playerId={command.PlayerId} itemId={command.ItemAssetId} cost={item.Cost} +str={item.AttackBonus} goldLeft={inventory.Gold} strengthNow={stats.Strength} items={inventory.ItemCount}");
  }

  private static bool TryGetPlayerHero(ref Frame frame, int playerId, out EntityRef heroEntity) {
    return UnitLookup.TryGetPlayerHero(ref frame, playerId, out heroEntity);
  }

  private static bool IsHeroNearTeamShop(ref Frame frame, EntityRef heroEntity) {
    if (!frame.Has<TeamComponent>(heroEntity) || !frame.Has<TransformComponent>(heroEntity))
      return false;

    var teamId = frame.GetReadOnly<TeamComponent>(heroEntity).TeamId;
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
    if (command.UnitIds.Count > 0) {
      ApplySelectedUnitTargets(ref frame, command, target);
      return;
    }

    ApplyLocalHeroTarget(ref frame, command.PlayerId, target);
  }

  private void HandleAttackCommand(ref Frame frame, AttackCommand command) {
    if (!CollectOrderedUnits(ref frame, command.PlayerId, command.UnitIds, _formationUnits, out var playerTeamId))
      return;

    if (!TryResolveAttackTarget(ref frame, command, playerTeamId, out var targetEntity))
      return;

    ref readonly var targetTransform = ref frame.GetReadOnly<TransformComponent>(targetEntity);
    for (var i = 0; i < _formationUnits.Count; i++) {
      var source = _formationUnits[i];
      SetAttackMoveTarget(ref frame, source.Entity, targetTransform.Position);
      SetAttackTarget(ref frame, source.Entity, command.TargetUnitId);
      frame.Logger.KDebug(
        $"[Combat] AttackCommand accepted tick={frame.Tick} playerId={command.PlayerId} sourceUnitId={source.UnitId} targetUnitId={command.TargetUnitId} moveTarget=({targetTransform.Position.x}, {targetTransform.Position.z})");
    }
  }

  private bool TryResolveAttackTarget(ref Frame frame, AttackCommand command, int playerTeamId,
    out EntityRef targetEntity) {
    if (!_unitIndex.TryGet(command.TargetUnitId, out targetEntity))
      return false;

    if (!frame.Has<TeamComponent>(targetEntity) || !frame.Has<Health>(targetEntity) ||
        !frame.Has<TransformComponent>(targetEntity))
      return false;

    ref readonly var health = ref frame.GetReadOnly<Health>(targetEntity);
    if (health.Current <= 0)
      return false;

    ref readonly var targetTeam = ref frame.GetReadOnly<TeamComponent>(targetEntity);
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
    if (!CollectOrderedUnits(ref frame, command.PlayerId, command.UnitIds, _formationUnits, out _))
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

  // Shared front half of every unit order: resolve the commanded ids to entities the issuing player
  // actually controls, leaving _unitIndex rebuilt for the caller. False means the order has nobody
  // to act on and should be dropped.
  private bool CollectOrderedUnits(ref Frame frame, int playerId, UnitIdList unitIds,
    List<FormationUnit> units, out int teamId) {
    units.Clear();
    if (!UnitLookup.TryGetPlayerTeamId(ref frame, playerId, out teamId))
      return false;

    _unitIndex.Rebuild(ref frame);
    for (var i = 0; i < unitIds.Count; i++) {
      if (!_unitIndex.TryGetControllableTeamUnitById(ref frame, teamId, unitIds[i], out var entity))
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
