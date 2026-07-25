using System;
using System.Collections.Generic;
using Godot;
using Meesles.Avalon.Client.Scripts.View;
using Meesles.Avalon.Sim.Commands;
using Meesles.Avalon.Sim.Models;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.Godot;

namespace Meesles.Avalon;

public class InputCapture : IDisposable {
  private const float DragSelectionThresholdPx = 6f;

  // Selection raycast: how far the mouse ray travels into the world, and a cap on how many stacked
  // pick colliders we skip past before giving up (front-most matching view wins).
  private const float PickRayLength = 1000f;
  private const int MaxPickIterations = 16;

  private readonly List<EntityViewNode> _selectedViews = new();
  private CameraController _camera;
  private Node3D _clickMarker;
  private Vector3 _clickMarkerBaseScale = Vector3.One;
  private Tween _clickMarkerTween;
  private Vector2 _dragCurrentScreen;
  private Vector2 _dragStartScreen;
  private EntityViewNode _fallbackFocusView;
  private FactionCatalog _factions;
  private GameUI _gameUI;
  private bool _isDraggingSelection;
  private bool _isLeftButtonDown;
  private int _localTeamId = 1;
  private AttackCommand _pendingAttackCommand;
  private MoveCommand _pendingMoveCommand;
  private PurchaseItemCommand _pendingPurchaseCommand;
  private ShopEntity _contextShop;
  private Node3D _singleplayerMoveTarget;
  private EntityViewUpdaterNode _viewRoot;

  public Vector3 SingleplayerTarget => _singleplayerMoveTarget?.GlobalPosition ?? Vector3.Zero;
  public bool HasSingleplayerTarget { get; private set; }

  public void Dispose() {
    ClearSelectedViews();
    _pendingMoveCommand = null;
    _pendingAttackCommand = null;
    _pendingPurchaseCommand = null;
    _contextShop = null;
    _camera = null;
    _gameUI?.SetSelectionRectangle(null);
    _gameUI?.SetContextShop(null);
    _gameUI = null;
    _viewRoot = null;
    _fallbackFocusView = null;
    _factions = null;
    _clickMarkerTween?.Kill();
    _clickMarkerTween = null;
    if (_clickMarker != null && GodotObject.IsInstanceValid(_clickMarker))
      _clickMarker.Visible = false;
    _clickMarker = null;
    _singleplayerMoveTarget = null;
  }

  public void BindCamera(CameraController camera) {
    _camera = camera;
  }

  public void BindGameUI(GameUI gameUI) {
    _gameUI = gameUI;
    // The action bar renders shop actions and calls back here when the player clicks a buy button;
    // we turn that into a pending PurchaseItemCommand that SimCallbacks.OnPollInput sends next tick.
    if (_gameUI != null)
      _gameUI.PurchaseRequested = QueuePurchase;
  }

  public void BindClickMarker(Node3D clickMarker) {
    _clickMarker = clickMarker;
    if (_clickMarker == null) return;

    _clickMarkerBaseScale = _clickMarker.Scale;
    _clickMarker.Visible = false;
  }

  public void BindViewRoot(EntityViewUpdaterNode viewRoot) {
    _viewRoot = viewRoot;
  }

  public void BindFactionCatalog(FactionCatalog factions) {
    _factions = factions;
  }

  public void BindSingleplayerMoveTarget(Node3D target) {
    _singleplayerMoveTarget = target;
  }

  public void SetLocalTeamId(int teamId) {
    _localTeamId = teamId;
  }

  public void CaptureInput() { }

  public bool TryConsumeMoveCommand(out MoveCommand command) {
    command = _pendingMoveCommand;
    _pendingMoveCommand = null;
    return command != null;
  }

  public bool TryConsumeAttackCommand(out AttackCommand command) {
    command = _pendingAttackCommand;
    _pendingAttackCommand = null;
    return command != null;
  }

  public bool TryConsumePurchaseCommand(out PurchaseItemCommand command) {
    command = _pendingPurchaseCommand;
    _pendingPurchaseCommand = null;
    return command != null;
  }

  // Called by the action bar when the player clicks a shop buy button. Validation (gold, range) is
  // the sim's job — we just forward the intent; a rejected purchase is simply a no-op in the sim.
  public void QueuePurchase(int itemAssetId) {
    _pendingPurchaseCommand = new PurchaseItemCommand { ItemAssetId = itemAssetId };
  }

  public void ClearSingleplayerTarget() {
    HasSingleplayerTarget = false;
  }

  public void SelectSingleView(EntityViewNode view) {
    if (view is IPlayerView)
      _fallbackFocusView = view;

    ApplySingleSelection(view);
  }

  public void HandleUnhandledInput(InputEvent @event) {
    if (_camera == null) return;

    switch (@event) {
      case InputEventMouseMotion motion:
        UpdateDragSelection(motion.Position);
        return;

      case InputEventMouseButton { ButtonIndex: MouseButton.Left } leftClick:
        if (leftClick.Pressed && IsPointerOverClickableUi()) return;
        HandleLeftClick(leftClick);
        return;

      case InputEventMouseButton { ButtonIndex: MouseButton.Right } rightClick:
        if (rightClick.Pressed && IsPointerOverClickableUi()) return;
        HandleRightClick(rightClick);
        return;
    }
  }

  // RTS world input is driven from the _Input phase (GameNode._Input), which runs before Control
  // nodes get the event. Without this guard a click on a HUD button (e.g. an action-bar buy button)
  // would ALSO drive world selection/movement - deselecting the shop and hiding the action grid on
  // the very click meant to buy. Only interactive widgets (BaseButton) capture the click; passive
  // HUD elements (labels, panels) aren't BaseButtons, so world clicks through empty HUD still work.
  private bool IsPointerOverClickableUi() {
    return _camera?.GetViewport()?.GuiGetHoveredControl() is BaseButton;
  }

  private void HandleLeftClick(InputEventMouseButton mouseButton) {
    if (mouseButton.Pressed)
      BeginDragSelection(mouseButton.Position);
    else
      EndDragSelection(mouseButton.Position);
  }

  private void HandleRightClick(InputEventMouseButton mouseButton) {
    if (!mouseButton.Pressed) return;

    if (TryGetEnemyUnitIdAt(mouseButton.Position, out var targetUnitId)) {
      QueueAttack(targetUnitId);
      return;
    }

    var ground = _camera.ScreenToGround(mouseButton.Position);
    if (ground == null) return;

    QueueMoveTo(ground.Value);
  }

  private void QueueMoveTo(Vector3 ground) {
    HasSingleplayerTarget = true;
    if (_singleplayerMoveTarget != null)
      _singleplayerMoveTarget.GlobalPosition = ground;

    PlayClickMarker(ground);

    var command = new MoveCommand {
      TargetX = FP64.FromFloat(ground.X),
      TargetZ = FP64.FromFloat(ground.Z)
    };

    foreach (var view in _selectedViews) {
      if (!TryGetUnitId(view, out var unitId)) continue;
      command.AddUnitId(unitId);
    }

    _pendingMoveCommand = command;
    _pendingAttackCommand = null;
  }

  private void QueueAttack(int targetUnitId) {
    var command = new AttackCommand { TargetUnitId = targetUnitId };

    foreach (var view in _selectedViews) {
      if (!TryGetUnitId(view, out var unitId)) continue;
      command.AddSourceUnitId(unitId);
    }

    if (command.SourceUnitIdCount == 0)
      return;

    _pendingAttackCommand = command;
    _pendingMoveCommand = null;
  }

  private void PlayClickMarker(Vector3 ground) {
    if (_clickMarker == null || !GodotObject.IsInstanceValid(_clickMarker)) return;

    _clickMarkerTween?.Kill();

    _clickMarker.GlobalPosition = new Vector3(ground.X, _clickMarker.GlobalPosition.Y, ground.Z);
    _clickMarker.Scale = _clickMarkerBaseScale;
    _clickMarker.Visible = true;

    _clickMarkerTween = _clickMarker.CreateTween();
    _clickMarkerTween.TweenProperty(_clickMarker, "scale", _clickMarkerBaseScale * 1.5f, 0.1)
      .SetTrans(Tween.TransitionType.Quad)
      .SetEase(Tween.EaseType.Out);
    _clickMarkerTween.TweenProperty(_clickMarker, "scale", Vector3.Zero, 0.25)
      .SetTrans(Tween.TransitionType.Quad)
      .SetEase(Tween.EaseType.In);
    _clickMarkerTween.TweenCallback(Callable.From(HideClickMarker));
  }

  private void HideClickMarker() {
    if (_clickMarker != null && GodotObject.IsInstanceValid(_clickMarker))
      _clickMarker.Visible = false;
  }

  private void BeginDragSelection(Vector2 screenPosition) {
    _isLeftButtonDown = true;
    _isDraggingSelection = false;
    _dragStartScreen = screenPosition;
    _dragCurrentScreen = screenPosition;
    _gameUI?.SetSelectionRectangle(null);
  }

  private void UpdateDragSelection(Vector2 screenPosition) {
    if (!_isLeftButtonDown) return;

    _dragCurrentScreen = screenPosition;
    if (!_isDraggingSelection && _dragStartScreen.DistanceTo(_dragCurrentScreen) < DragSelectionThresholdPx)
      return;

    _isDraggingSelection = true;
    _gameUI?.SetSelectionRectangle(GetSelectionRectangle(_dragStartScreen, _dragCurrentScreen));
  }

  private void EndDragSelection(Vector2 screenPosition) {
    if (!_isLeftButtonDown) return;

    _dragCurrentScreen = screenPosition;
    var wasDragging = _isDraggingSelection;
    _isLeftButtonDown = false;
    _isDraggingSelection = false;
    _gameUI?.SetSelectionRectangle(null);

    if (wasDragging)
      SelectOwnedViewsInRectangle(GetSelectionRectangle(_dragStartScreen, _dragCurrentScreen));
    else
      SelectNearestOwnedView(screenPosition);
  }

  private void SelectNearestOwnedView(Vector2 screenPosition) {
    ApplySingleSelection(PickView(screenPosition, CanClickSelectView) ?? GetFallbackFocusView());
  }

  // Raycast the mouse ray against each view's selection capsule (EntityViewPhysics.SelectionLayer) and
  // return the nearest hit that passes the filter, skipping through hits that don't (e.g. a friendly unit
  // standing in front of the enemy you right-clicked). Clicking anywhere on a unit's silhouette selects
  // it, and IntersectRay's front-to-back ordering gives correct overlap priority for free.
  private EntityViewNode PickView(Vector2 screenPosition, Func<EntityViewNode, bool> filter) {
    if (_camera == null)
      return null;

    var space = _camera.GetWorld3D()?.DirectSpaceState;
    if (space == null)
      return null;

    var origin = _camera.ProjectRayOrigin(screenPosition);
    var query = PhysicsRayQueryParameters3D.Create(
      origin, origin + _camera.ProjectRayNormal(screenPosition) * PickRayLength);
    query.CollisionMask = EntityViewPhysics.SelectionLayer;
    query.CollideWithAreas = true;
    query.CollideWithBodies = false;

    var exclude = new Godot.Collections.Array<Rid>();
    for (var i = 0; i < MaxPickIterations; i++) {
      var hit = space.IntersectRay(query);
      if (hit.Count == 0)
        return null;

      var view = FindOwningView(hit["collider"].As<GodotObject>() as Node);
      if (view != null && filter(view))
        return view;

      exclude.Add(hit["rid"].As<Rid>());
      query.Exclude = exclude;
    }

    return null;
  }

  private static EntityViewNode FindOwningView(Node node) {
    while (node != null) {
      if (node is EntityViewNode view)
        return view;
      node = node.GetParent();
    }

    return null;
  }

  private void SelectOwnedViewsInRectangle(Rect2 rectangle) {
    ClearSelectedViews();
    if (_viewRoot == null || _camera == null) return;

    foreach (var child in _viewRoot.GetChildren()) {
      if (child is not EntityViewNode view) continue;
      if (!CanSelectView(view)) continue;

      var screen = _camera.UnprojectPosition(view.GlobalPosition);
      if (!rectangle.HasPoint(screen)) continue;

      _selectedViews.Add(view);
      SetSelectionIndicator(view, true);
    }

    // An empty box would otherwise leave nothing selected; fall back to the player's main hero so the
    // selection/focus is never empty, matching the single-click behaviour.
    if (_selectedViews.Count == 0) {
      ApplySingleSelection(GetFallbackFocusView());
      return;
    }

    UpdateFocusPortrait();
  }

  private void ClearSelectedViews() {
    foreach (var view in _selectedViews)
      SetSelectionIndicator(view, false);
    _selectedViews.Clear();
  }

  private void ApplySingleSelection(EntityViewNode view) {
    ClearSelectedViews();
    if (view == null) {
      UpdateFocusPortrait();
      return;
    }

    _selectedViews.Add(view);
    SetSelectionIndicator(view, true);
    UpdateFocusPortrait();
  }

  // Map focus state to the rendered portrait in the UI
  private void UpdateFocusPortrait() {
    if (_gameUI == null) return;

    UpdateContextShop();

    // Prefer the player's champion when it's part of the selection so a mixed group (hero + minions)
    // always shows the hero portrait, rather than whatever unit happens to be first in the list.
    var championView = GetSelectedChampionView();
    var view = championView ?? (_selectedViews.Count > 0 ? _selectedViews[0] : null);

    // Named props/structures (turret, crystal, shop, fountain, pickup) show their own label. If the
    // named view also resolves a faction (not currently the case for these) we reuse its portrait,
    // otherwise the label stands alone with no portrait texture.
    if (view is INamedView named) {
      Texture2D portrait = null;
      if (_factions != null && TryResolveHeroFactionId(view, out var namedFactionId))
        portrait = _factions.Resolve(namedFactionId).PortraitTexture;
      _gameUI.SetFocusPortrait(portrait, named.DisplayName);
      return;
    }

    if (_factions != null && view != null && TryResolveHeroFactionId(view, out var factionId)) {
      var entry = _factions.Resolve(factionId);
      var label = entry.DisplayName;

      // When the champion is the rendered portrait and minions are selected alongside it, surface
      // the extra unit count next to the name (e.g. "Merlin +20").
      if (championView != null) {
        var minionCount = CountSelectedMinions();
        if (minionCount > 0)
          label = $"{label} +{minionCount}";
      }

      _gameUI.SetFocusPortrait(entry.PortraitTexture, label);
      return;
    }

    _gameUI.SetFocusPortrait(null, null);
  }

  // A shop is "in context" for the action bar only when it's the sole selection (single-click
  // inspect). Selecting your own units clears it. The action bar re-checks proximity every frame,
  // so this just tells it which shop (if any) the player is looking at.
  private void UpdateContextShop() {
    var shop = _selectedViews.Count == 1 && _selectedViews[0] is ShopEntity s ? s : null;
    if (ReferenceEquals(shop, _contextShop)) return;

    _contextShop = shop;
    _gameUI?.SetContextShop(shop);
  }

  // The player's champion (hero) if one is part of the current selection.
  private EntityViewNode GetSelectedChampionView() {
    foreach (var view in _selectedViews)
      if (IsHeroView(view))
        return view;

    return null;
  }

  // Count of selected controllable units that aren't the champion, i.e. the minion tail of a
  // mixed selection. Structures/props (no unit id) don't count.
  private int CountSelectedMinions() {
    var count = 0;
    foreach (var view in _selectedViews)
      if (!IsHeroView(view) && TryGetUnitId(view, out _))
        count++;

    return count;
  }

  private static bool IsHeroView(EntityViewNode view) {
    if (view.Engine == null || !view.EntityRef.IsValid)
      return false;

    var frame = view.Engine.PredictedFrame.Frame;
    if (frame == null || !frame.Has<Hero>(view.EntityRef))
      frame = view.Engine.VerifiedFrame.Frame;

    return frame != null && frame.Has<Hero>(view.EntityRef);
  }

  private static bool TryResolveHeroFactionId(EntityViewNode view, out int factionId) {
    factionId = 0;
    if (view.Engine == null || !view.EntityRef.IsValid) return false;

    var frame = view.Engine.PredictedFrame.Frame;
    if (frame == null || !frame.Has<Hero>(view.EntityRef))
      frame = view.Engine.VerifiedFrame.Frame;

    if (frame == null || !frame.Has<Hero>(view.EntityRef) || !frame.Has<Faction>(view.EntityRef))
      return false;

    factionId = frame.GetReadOnly<Faction>(view.EntityRef).FactionId;
    return true;
  }

  private EntityViewNode GetFallbackFocusView() {
    if (_fallbackFocusView == null || !GodotObject.IsInstanceValid(_fallbackFocusView))
      return null;

    return _fallbackFocusView;
  }

  private static void SetSelectionIndicator(EntityViewNode view, bool selected) {
    if (view == null || !GodotObject.IsInstanceValid(view)) return;
    var indicator = view.GetNodeOrNull<SelectionIndicator>("SelectionIndicator");
    indicator?.SetSelected(selected);
  }

  private bool TryGetEnemyUnitIdAt(Vector2 screenPosition, out int unitId) {
    unitId = 0;
    var target = PickView(screenPosition, IsEnemyUnitView);
    return target != null && TryGetUnitId(target, out unitId);
  }

  private bool IsEnemyUnitView(EntityViewNode view) {
    return !ViewTeamMatches(view) && TryGetUnitId(view, out _);
  }

  private bool ViewTeamMatches(EntityViewNode view) {
    return view is ISelectableTeamView selectable && selectable.TeamMatches(_localTeamId);
  }

  private bool CanSelectView(EntityViewNode view) {
    return ViewTeamMatches(view) && IsControllableView(view);
  }

  // Single-click selection is broader than command selection: besides the player's own controllable
  // units, any named view (structures, props, pickups) can be clicked to inspect it and surface its
  // name in the focus portrait. Box-select and move/attack commands still use CanSelectView, so
  // inspecting a structure never pulls it into a command group.
  private bool CanClickSelectView(EntityViewNode view) {
    return CanSelectView(view) || view is INamedView;
  }

  private static Rect2 GetSelectionRectangle(Vector2 start, Vector2 end) {
    Vector2 position = new(Mathf.Min(start.X, end.X), Mathf.Min(start.Y, end.Y));
    Vector2 size = new(Mathf.Abs(end.X - start.X), Mathf.Abs(end.Y - start.Y));
    return new Rect2(position, size);
  }

  private static bool TryGetUnitId(EntityViewNode view, out int unitId) {
    unitId = 0;
    if (view.Engine == null || !view.EntityRef.IsValid)
      return false;

    var frame = view.Engine.PredictedFrame.Frame;
    if (frame == null || !frame.Has<Unit>(view.EntityRef))
      frame = view.Engine.VerifiedFrame.Frame;

    if (frame == null || !frame.Has<Unit>(view.EntityRef))
      return false;

    unitId = frame.GetReadOnly<Unit>(view.EntityRef).UnitId;
    return true;
  }

  private static bool IsControllableView(EntityViewNode view) {
    if (view.Engine == null || !view.EntityRef.IsValid)
      return false;

    var frame = view.Engine.PredictedFrame.Frame;
    if (frame == null || !frame.Has<Unit>(view.EntityRef))
      frame = view.Engine.VerifiedFrame.Frame;

    return frame != null
           && frame.Has<Unit>(view.EntityRef)
           && frame.Has<Controllable>(view.EntityRef);
  }
}
