using System;
using System.Collections.Generic;
using Godot;
using Meesles.Avalon.Client.Scripts.View;
using Meesles.Avalon.Sim;
using Meesles.Avalon.Sim.Commands;
using Meesles.Avalon.Sim.Components;
using Meesles.Avalon.Sim.Heroes;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.Deterministic.Navigation;
using xpTURN.Klotho.Godot;
using IKlothoEngine = xpTURN.Klotho.Core.IKlothoEngine;   // Klotho.Core also defines MoveCommand

namespace Meesles.Avalon;

public class InputCapture : IDisposable {
  private const float DragSelectionThresholdPx = 6f;

  // Move-order dedup: how long a just-issued target keeps swallowing repeats, and how close a click
  // has to be to count as the same target. ~200ms at 30Hz - long enough for a double-click, short
  // enough that it never eats a deliberate re-order.
  private const int MoveDedupWindowTicks = 6;
  private const float MoveDedupEpsilonSqr = 0.0025f;   // 0.05 units

  // Selection raycast: how far the mouse ray travels into the world, and a cap on how many stacked
  // pick colliders we skip past before giving up (front-most matching view wins).
  private const float PickRayLength = 1000f;
  private const int MaxPickIterations = 16;

  // Match the sim's FPNavAgentSystem.MultiFloorYThreshold so client-side move-target snapping treats
  // the same vertical steps as walls that the authoritative navigation does.
  private static readonly FP64 NavMultiFloorYThreshold = FP64.FromDouble(2.0);

  // Planar distance (squared) under which a MoveAlongSurface result is treated as "didn't move" —
  // its bounded BFS returns the start point when it never reaches a wall, which we must not snap to.
  private static readonly FP64 NavSnapNoMoveSqr = FP64.FromDouble(0.01);

  private readonly List<EntityViewNode> _selectedViews = new();
  private int _aimingSlot = -1;
  private SkillTelegraphManager _telegraphs;
  private CameraController _camera;
  private Node3D _clickMarker;
  private Vector3 _clickMarkerBaseScale = Vector3.One;
  private Tween _clickMarkerTween;
  private Vector2 _dragCurrentScreen;
  private Vector2 _dragStartScreen;
  private EntityViewNode _fallbackFocusView;
  private FactionCatalog _factions;
  private IKlothoEngine _engine;
  private FPNavMesh _navMesh;
  private FPNavMeshQuery _navQuery;
  private GameUI _gameUI;
  private bool _isDraggingSelection;
  private bool _isLeftButtonDown;
  private int _localTeamId = 1;
  private AttackCommand _pendingAttackCommand;
  private MoveCommand _pendingMoveCommand;
  private Vector3 _lastMoveTarget;
  private int _lastMoveOrderTick = int.MinValue;
  private PurchaseItemCommand _pendingPurchaseCommand;
  private readonly Queue<UpgradeSkillCommand> _pendingUpgradeSkillCommands = new();
  private CastSkillCommand _pendingCastSkillCommand;
  private ShopEntity _contextShop;
  private Node3D _singleplayerMoveTarget;
  private EntityViewUpdaterNode _viewRoot;

  public Vector3 SingleplayerTarget => _singleplayerMoveTarget?.GlobalPosition ?? Vector3.Zero;
  public bool HasSingleplayerTarget { get; private set; }

  public void Dispose() {
    ClearSelectedViews();
    CancelSkillAim();
    _telegraphs = null;
    _pendingMoveCommand = null;
    _pendingAttackCommand = null;
    _pendingPurchaseCommand = null;
    _pendingUpgradeSkillCommands.Clear();
    _pendingCastSkillCommand = null;
    _contextShop = null;
    _camera = null;
    _gameUI?.SetSelectionRectangle(null);
    _gameUI?.SetContextShop(null);
    _gameUI = null;
    _viewRoot = null;
    _fallbackFocusView = null;
    _factions = null;
    _engine = null;
    _navMesh = null;
    _navQuery = null;
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
    if (_gameUI != null) {
      _gameUI.PurchaseRequested = QueuePurchase;
      _gameUI.SkillUpgradeRequested = QueueSkillUpgrade;
      _gameUI.SkillCastRequested = QueueSkillCast;
    }
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

  // Predicted frame + local player id, so a queued command can be checked against the sim's own rules
  // before it goes out (see QueueSkillCast).
  public void BindEngine(IKlothoEngine engine) {
    _engine = engine;
  }

  // Read-only navmesh + query used to keep right-click move targets on walkable ground (see
  // SnapMoveTargetToWalkable). A separate instance from the sim's — it only reads, never mutates.
  public void BindNavigation(FPNavMesh navMesh, FPNavMeshQuery navQuery) {
    _navMesh = navMesh;
    _navQuery = navQuery;
  }

  public void BindSingleplayerMoveTarget(Node3D target) {
    _singleplayerMoveTarget = target;
  }

  public void BindTelegraphs(SkillTelegraphManager telegraphs) {
    CancelSkillAim();
    _telegraphs = telegraphs;
  }

  public void SetLocalTeamId(int teamId) {
    _localTeamId = teamId;
  }

  // Per-frame while a skill key is held, so the preview tracks the cursor and the moving caster, and
  // appears the moment a slot that was cooling becomes castable mid-hold.
  public void CaptureInput() {
    if (_aimingSlot < 0) return;

    if (_camera == null || _camera.IsGodmode) {
      CancelSkillAim();
      return;
    }

    if (CanAct(_aimingSlot, SkillAction.Cast))
      _telegraphs?.ShowAim(_aimingSlot, GetSkillAimPoint());
    else
      _telegraphs?.HideAim();
  }

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

  public bool TryConsumeUpgradeSkillCommand(out UpgradeSkillCommand command) {
    return _pendingUpgradeSkillCommands.TryDequeue(out command);
  }

  public bool TryConsumeCastSkillCommand(out CastSkillCommand command) {
    command = _pendingCastSkillCommand;
    _pendingCastSkillCommand = null;
    return command != null;
  }

  public void QueuePurchase(int itemAssetId) {
    if (!CanPurchase(itemAssetId)) return;
    _pendingPurchaseCommand = new PurchaseItemCommand { ItemAssetId = itemAssetId };
  }

  // Klotho takes one command per player per tick, so rapid clicks queue rather than overwrite - at
  // 30Hz two clicks inside 33ms are ordinary, and dropping the second reads as an eaten button.
  public void QueueSkillUpgrade(int slot) {
    if (!CanAct(slot, SkillAction.Upgrade)) return;

    _pendingUpgradeSkillCommands.Enqueue(new UpgradeSkillCommand { Slot = slot });
    _gameUI?.PredictedSkills.PredictUpgrade(slot);
    RepaintHud();
  }

  // The HUD only syncs on an executed sim tick, so an optimistic change made between ticks would still
  // wait up to a tick to show. Push the frame through now instead - the same sync the engine drives.
  private void RepaintHud() {
    var frame = _engine?.PredictedFrame.Frame;
    if (frame != null) _gameUI?.SyncFromFrame(frame);
  }

  // Every cast carries the cursor's ground point
  public void QueueSkillCast(int slot) {
    if (!CanAct(slot, SkillAction.Cast)) return;

    var aim = GetSkillAimPoint();
    _pendingCastSkillCommand = new CastSkillCommand {
      Slot = slot,
      TargetX = FP64.FromFloat(aim.X),
      TargetZ = FP64.FromFloat(aim.Z)
    };
  }

  // Asked against the predicted frame with the sim's own predicate, so a dead hero, an unlearned slot or
  // one still cooling never costs a round trip. The sim judges the command again when it lands - this
  // only skips the ones already known to fail. Without an engine bound (or before the hero exists) the
  // command goes out and the sim decides, which is the pre-existing behaviour.
  // The sim keeps ticking through Klotho's post-match grace window, so an order issued after the
  // winner is decided would still execute. Input stops at the source rather than in each handler.
  private bool MatchEnded {
    get {
      var frame = _engine?.PredictedFrame.Frame;
      return frame != null &&
             frame.TryGetSingleton<MatchOutcome>(out var entity) &&
             frame.GetReadOnly<MatchOutcome>(entity).Ended;
    }
  }

  private bool CanAct(int slot, SkillAction action) {
    var frame = _engine?.PredictedFrame.Frame;
    if (frame == null) return true;
    if (MatchEnded) return false;

    // Asked against the upgrades already queued too, so a slot ranked up a tick ago is castable now
    // rather than after the frame catches up, and spending the last point twice is refused here rather
    // than sent and rejected.
    var predicted = _gameUI?.PredictedSkills;
    var pendingRanks = predicted?.OutstandingFor(slot) ?? 0;

    return action == SkillAction.Cast
      ? SkillActions.CanCast(ref frame, _engine.LocalPlayerId, slot, pendingRanks)
      : SkillActions.CanUpgrade(ref frame, _engine.LocalPlayerId, slot,
        predicted?.PendingPoints ?? 0, pendingRanks);
  }

  // Same deal for buys: an unaffordable, out-of-range or unknown item is dropped here rather than sent
  // and rejected. ActionBarController greys those buttons off the same predicate.
  private bool CanPurchase(int itemAssetId) {
    var frame = _engine?.PredictedFrame.Frame;
    if (frame == null) return true;
    if (MatchEnded) return false;

    return ShopActions.CanPurchase(ref frame, _engine.LocalPlayerId, itemAssetId);
  }

  private enum SkillAction {
    Cast,
    Upgrade
  }

  // A ground pick only fails when the camera is looking parallel to the ground, which this top-down
  // rig never does. Falling back to the caster's own position keeps the cast alive rather than
  // aiming it at the map origin: self-cast skills don't care, and the sim reads a zero-length aim
  // as "fire along my facing".
  private Vector3 GetSkillAimPoint() {
    if (_camera == null)
      return Vector3.Zero;

    var ground = _camera.ScreenToGround(_camera.GetViewport().GetMousePosition());
    if (ground != null)
      return ground.Value;

    var hero = GetFallbackFocusView();
    return hero != null ? hero.GlobalPosition : Vector3.Zero;
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
    if (_camera == null || MatchEnded) return;

    switch (@event) {
      case InputEventKey { Echo: false } key when TryGetSkillHotkeySlot(key.Keycode, out var slot):
        if (_camera.IsGodmode) return;
        if (key.Pressed) BeginSkillAim(slot);
        else ReleaseSkillAim(slot);
        return;

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

  // Hold to aim, release to cast. The slot is tracked from key-down regardless of CanAct so a skill that
  // comes off cooldown while held still previews and still fires; both ends re-ask before anything is
  // drawn or queued. The bar's click-to-cast path stays a single instant cast.
  private void BeginSkillAim(int slot) {
    _aimingSlot = slot;
    if (CanAct(slot, SkillAction.Cast))
      _telegraphs?.ShowAim(slot, GetSkillAimPoint());
  }

  private void ReleaseSkillAim(int slot) {
    if (_aimingSlot != slot) return;
    CancelSkillAim();
    QueueSkillCast(slot);
  }

  private void CancelSkillAim() {
    _aimingSlot = -1;
    _telegraphs?.HideAim();
  }

  // Q/W/E/R map to the four SkillSlots in order, matching the skill cells the bar renders left to right.
  // Camera panning was moved off WASD onto the arrow keys so W is free (CameraController.NormalMoveDirection);
  // godmode's flycam still uses WASD+QE, which is why the caller drops these while it's on.
  private static bool TryGetSkillHotkeySlot(Key keycode, out int slot) {
    switch (keycode) {
      case Key.Q:
        slot = (int)SkillSlot.Primary;
        return true;
      case Key.W:
        slot = (int)SkillSlot.Secondary;
        return true;
      case Key.E:
        slot = (int)SkillSlot.Tertiary;
        return true;
      case Key.R:
        slot = (int)SkillSlot.Ultimate;
        return true;
      default:
        slot = -1;
        return false;
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

    QueueMoveTo(SnapMoveTargetToWalkable(ground.Value));
  }

  // A right-click can land on a spot that isn't walkable, and the raw point is then unreachable so
  // the move is silently dropped. Three off-mesh cases matter:
  //   - inside a structure/obstacle footprint (shop, trees) that carves a hole in the navmesh,
  //   - inside an obstacle "island" surrounded by walkable ground,
  //   - entirely off the map, past the navmesh's outer edge.
  private Vector3 SnapMoveTargetToWalkable(Vector3 ground) {
    if (_navQuery == null || _navMesh == null)
      return ground;

    var clickXZ = new FPVector2(FP64.FromFloat(ground.X), FP64.FromFloat(ground.Z));

    // Already on walkable navmesh: keep the exact click. A* routes around any obstacles in between.
    if (_navQuery.FindTriangle(clickXZ) >= 0)
      return ground;

    // Stage 1: pull a far-off-map click onto the map's extent. A click well past the edge has no
    // nearby grid cells to search, so the closest-point snap below would find nothing; clamping to
    // BoundsXZ lands it on the perimeter where the boundary triangles actually are. In-bounds
    // clicks (obstacle islands) are already inside the box, so this leaves them untouched.
    var boundedXZ = _navMesh.BoundsXZ.ClosestPoint(clickXZ);

    // Stage 2: direction-aware snap anchored on the unit doing the moving.
    if (TryGetNavStart(GetMoveOriginView(), out var startPos, out var startTri)) {
      var endPos = new FPVector3(boundedXZ.x, startPos.y, boundedXZ.y);
      var (resultPos, resultTri) =
        _navQuery.MoveAlongSurface(startPos, endPos, startTri, NavMultiFloorYThreshold);

      // MoveAlongSurface returns the start point when its bounded BFS never reaches a wall (open
      // ground far from the target); treat that as "no useful snap" and fall through to stage 3.
      var moved = FPVector2.SqrDistance(startPos.ToXZ(), resultPos.ToXZ()) > NavSnapNoMoveSqr;
      if (resultTri >= 0 && moved)
        return new Vector3(resultPos.x.ToFloat(), ground.Y, resultPos.z.ToFloat());
    }

    // Stage 3: geometrically nearest walkable point to the (bounded) click. Handles the no-origin
    // case and far clicks the walk couldn't reach — snaps onto the closest mesh edge.
    var closest = _navQuery.ClosestPointOnNavMesh(boundedXZ, out var closestTri);
    return closestTri >= 0
      ? new Vector3(closest.x.ToFloat(), ground.Y, closest.y.ToFloat())
      : ground;
  }

  // The unit whose approach direction anchors target snapping: the selected hero if present,
  // else the first selected unit, else the focus hero (covers the no-selection hero move where the
  // command carries no unit ids and the sim moves the player's own hero).
  private EntityViewNode GetMoveOriginView() {
    var hero = GetSelectedHeroView();
    if (hero != null)
      return hero;
    if (_selectedViews.Count > 0)
      return _selectedViews[0];

    return GetFallbackFocusView();
  }

  // Resolves the navmesh triangle the given view currently stands on, used as the BFS origin for
  // direction-aware target snapping. Projects onto the mesh in case the view sits slightly off it.
  private bool TryGetNavStart(EntityViewNode view, out FPVector3 startPos, out int startTri) {
    startPos = FPVector3.Zero;
    startTri = -1;
    if (view == null || !GodotObject.IsInstanceValid(view))
      return false;

    var pos = view.GlobalPosition;
    var xz = new FPVector2(FP64.FromFloat(pos.X), FP64.FromFloat(pos.Z));
    var snapped = _navQuery.ClosestPointOnNavMesh(xz, out startTri);
    if (startTri < 0)
      return false;

    startPos = new FPVector3(snapped.x, FP64.Zero, snapped.y);
    return true;
  }

  private void QueueMoveTo(Vector3 ground) {
    HasSingleplayerTarget = true;
    if (_singleplayerMoveTarget != null)
      _singleplayerMoveTarget.GlobalPosition = ground;

    // The marker still plays on a deduped click - the order was understood, it just says the same
    // thing as the one already in flight.
    PlayClickMarker(ground);
    if (IsRedundantMoveOrder(ground)) return;

    var command = new MoveCommand {
      TargetX = FP64.FromFloat(ground.X),
      TargetZ = FP64.FromFloat(ground.Z)
    };

    foreach (var view in _selectedViews) {
      if (!TryGetUnitId(view, out var unitId)) continue;
      command.UnitIds.Add(unitId);
    }

    _pendingMoveCommand = command;
    _pendingAttackCommand = null;
    _lastMoveTarget = ground;
    _lastMoveOrderTick = _engine?.CurrentTick ?? int.MinValue;
  }

  // Spam-clicking the same spot sends one MoveCommand per click, each one burning that tick's single
  // command slot to re-issue an order the unit is already following. Bounded by a window rather than
  // remembered indefinitely: a click on the same spot seconds later is a real order - the unit may
  // have arrived, been displaced, or had the order overridden since - and must still go out.
  private bool IsRedundantMoveOrder(Vector3 ground) {
    if (_lastMoveOrderTick == int.MinValue) return false;
    if (_engine == null) return false;

    var age = _engine.CurrentTick - _lastMoveOrderTick;
    if (age < 0 || age > MoveDedupWindowTicks) return false;

    return ground.DistanceSquaredTo(_lastMoveTarget) <= MoveDedupEpsilonSqr;
  }

  private void QueueAttack(int targetUnitId) {
    var command = new AttackCommand { TargetUnitId = targetUnitId };

    foreach (var view in _selectedViews) {
      if (!TryGetUnitId(view, out var unitId)) continue;
      command.UnitIds.Add(unitId);
    }

    if (command.UnitIds.Count == 0)
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

    // Every selection change routes through here. The same point ordered for a different unit set is
    // a different order, so the dedup window must not carry across one.
    _lastMoveOrderTick = int.MinValue;
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

    // Prefer the player's hero portrait
    var heroView = GetSelectedHeroView();
    var view = heroView ?? (_selectedViews.Count > 0 ? _selectedViews[0] : null);

    // Named props/structures (turret, crystal, shop, fountain, pickup) show their own label.
    // Factions show theirs. Fallback to 'todo' image
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

      // When the hero is the rendered portrait and minions are selected alongside it, surface
      // the extra unit count next to the name (e.g. "Merlin +20").
      if (heroView != null) {
        var minionCount = CountSelectedMinions();
        if (minionCount > 0)
          label = $"{label} +{minionCount}";
      }

      _gameUI.SetFocusPortrait(entry.PortraitTexture, label);
      return;
    }

    _gameUI.SetFocusPortrait(null, null);
  }

  // Only applied when the shop is sole selected
  private void UpdateContextShop() {
    var shop = _selectedViews.Count == 1 && _selectedViews[0] is ShopEntity s ? s : null;
    if (ReferenceEquals(shop, _contextShop)) return;

    _contextShop = shop;
    _gameUI?.SetContextShop(shop);
  }

  // The player's hero if one is part of the current selection.
  private EntityViewNode GetSelectedHeroView() {
    foreach (var view in _selectedViews)
      if (IsHeroView(view))
        return view;

    return null;
  }

  // Count of selected controllable units that aren't the hero, i.e. the minion tail of a
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

    if (frame == null || !frame.Has<Hero>(view.EntityRef) || !frame.Has<FactionComponent>(view.EntityRef))
      return false;

    factionId = frame.GetReadOnly<FactionComponent>(view.EntityRef).FactionId;
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
    if (frame == null || !frame.Has<UnitIdComponent>(view.EntityRef))
      frame = view.Engine.VerifiedFrame.Frame;

    if (frame == null || !frame.Has<UnitIdComponent>(view.EntityRef))
      return false;

    unitId = frame.GetReadOnly<UnitIdComponent>(view.EntityRef).UnitId;
    return true;
  }

  private static bool IsControllableView(EntityViewNode view) {
    if (view.Engine == null || !view.EntityRef.IsValid)
      return false;

    var frame = view.Engine.PredictedFrame.Frame;
    if (frame == null || !frame.Has<UnitIdComponent>(view.EntityRef))
      frame = view.Engine.VerifiedFrame.Frame;

    return frame != null
           && frame.Has<UnitIdComponent>(view.EntityRef)
           && frame.Has<Controllable>(view.EntityRef);
  }
}
