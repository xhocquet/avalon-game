using System;
using System.Collections.Generic;
using global::Godot;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.Godot;
using Meesles.Avalon.Sim.Commands;
using Meesles.Avalon.Sim.Models;

namespace Meesles.Avalon {
  public class InputCapture : IDisposable {
    private const float DragSelectionThresholdPx = 6f;

    private readonly List<EntityViewNode> _selectedViews = new();
    private CameraController _camera;
    private GameUI _gameUI;
    private EntityViewUpdaterNode _viewRoot;
    private EntityViewNode _fallbackFocusView;
    private MoveCommand _pendingMoveCommand;
    private Node3D _singleplayerMoveTarget;
    private Vector2 _dragStartScreen;
    private Vector2 _dragCurrentScreen;
    private bool _hasSingleplayerTarget;
    private bool _isLeftButtonDown;
    private bool _isDraggingSelection;
    private int _localTeamId = 1;

    public Vector3 SingleplayerTarget => _singleplayerMoveTarget?.GlobalPosition ?? Vector3.Zero;
    public bool HasSingleplayerTarget => _hasSingleplayerTarget;

    public void BindCamera(CameraController camera) {
      _camera = camera;
    }

    public void BindGameUI(GameUI gameUI) {
      _gameUI = gameUI;
    }

    public void BindViewRoot(EntityViewUpdaterNode viewRoot) {
      _viewRoot = viewRoot;
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

    public void ClearSingleplayerTarget() {
      _hasSingleplayerTarget = false;
    }

    public void SelectSingleView(EntityViewNode view) {
      if (view is IPlayerView)
        _fallbackFocusView = view;

      ApplySingleSelection(view);
    }

    public void HandleUnhandledInput(InputEvent @event) {
      if (_camera == null) return;

      if (@event is InputEventMouseMotion motion) {
        UpdateDragSelection(motion.Position);
        return;
      }

      if (@event is not InputEventMouseButton mouseButton) return;

      if (mouseButton.ButtonIndex == MouseButton.Left) {
        if (mouseButton.Pressed)
          BeginDragSelection(mouseButton.Position);
        else
          EndDragSelection(mouseButton.Position);
        return;
      }

      if (mouseButton.ButtonIndex != MouseButton.Right || !mouseButton.Pressed) return;

      Vector3? ground = _camera.ScreenToGround(mouseButton.Position);
      if (ground == null) return;

      QueueMoveTo(ground.Value);
    }

    private void QueueMoveTo(Vector3 ground) {
      _hasSingleplayerTarget = true;
      if (_singleplayerMoveTarget != null)
        _singleplayerMoveTarget.GlobalPosition = ground;

      var command = new MoveCommand {
        TargetX = FP64.FromFloat(ground.X),
        TargetZ = FP64.FromFloat(ground.Z),
      };

      foreach (var view in _selectedViews) {
        if (!TryGetUnitId(view, out int unitId)) continue;
        command.AddUnitId(unitId);
      }

      _pendingMoveCommand = command;
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
      bool wasDragging = _isDraggingSelection;
      _isLeftButtonDown = false;
      _isDraggingSelection = false;
      _gameUI?.SetSelectionRectangle(null);

      if (wasDragging)
        SelectOwnedViewsInRectangle(GetSelectionRectangle(_dragStartScreen, _dragCurrentScreen));
      else
        SelectNearestOwnedView(screenPosition);
    }

    private void SelectNearestOwnedView(Vector2 screenPosition) {
      if (_viewRoot == null || _camera == null) {
        ApplySingleSelection(GetFallbackFocusView());
        return;
      }

      EntityViewNode best = null;
      float bestDistSqr = 22f * 22f;

      foreach (Node child in _viewRoot.GetChildren()) {
        if (child is not EntityViewNode view) continue;
        if (!ViewTeamMatches(view)) continue;

        Vector2 screen = _camera.UnprojectPosition(view.GlobalPosition);
        float distSqr = screen.DistanceSquaredTo(screenPosition);
        if (distSqr >= bestDistSqr) continue;

        best = view;
        bestDistSqr = distSqr;
      }

      ApplySingleSelection(best ?? GetFallbackFocusView());
    }

    private void SelectOwnedViewsInRectangle(Rect2 rectangle) {
      ClearSelectedViews();
      if (_viewRoot == null || _camera == null) return;

      foreach (Node child in _viewRoot.GetChildren()) {
        if (child is not EntityViewNode view) continue;
        if (!ViewTeamMatches(view)) continue;

        Vector2 screen = _camera.UnprojectPosition(view.GlobalPosition);
        if (!rectangle.HasPoint(screen)) continue;

        _selectedViews.Add(view);
        SetSelectionIndicator(view, true);
      }
    }

    private void ClearSelectedViews() {
      foreach (var view in _selectedViews)
        SetSelectionIndicator(view, false);
      _selectedViews.Clear();
    }

    private void ApplySingleSelection(EntityViewNode view) {
      ClearSelectedViews();
      if (view == null) return;

      _selectedViews.Add(view);
      SetSelectionIndicator(view, true);
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

    private bool ViewTeamMatches(EntityViewNode view) {
      return view is ISelectableTeamView selectable && selectable.TeamMatches(_localTeamId);
    }

    private static Rect2 GetSelectionRectangle(Vector2 start, Vector2 end) {
      Vector2 position = new(Mathf.Min(start.X, end.X), Mathf.Min(start.Y, end.Y));
      Vector2 size = new(Mathf.Abs(end.X - start.X), Mathf.Abs(end.Y - start.Y));
      return new Rect2(position, size);
    }

    private static bool TryGetUnitId(EntityViewNode view, out int unitId) {
      unitId = 0;
      var frame = view.Engine?.PredictedFrame.Frame;
      if (frame == null || !view.EntityRef.IsValid || !frame.Has<Unit>(view.EntityRef)) return false;

      unitId = frame.GetReadOnly<Unit>(view.EntityRef).UnitId;
      return true;
    }

    public void Dispose() {
      ClearSelectedViews();
      _pendingMoveCommand = null;
      _camera = null;
      _gameUI?.SetSelectionRectangle(null);
      _gameUI = null;
      _viewRoot = null;
      _fallbackFocusView = null;
      _singleplayerMoveTarget = null;
    }
  }
}
