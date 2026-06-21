using System;
using System.Collections.Generic;
using global::Godot;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.Godot;
using Meesles.Avalon.Sim.Commands;
using Meesles.Avalon.Sim.Models;

namespace Meesles.Avalon {
  public class InputCapture : IDisposable {
    private const int MaxSelectedUnitIds = 8;

    private readonly List<EntityViewNode> _selectedViews = new();
    private CameraController _camera;
    private EntityViewUpdaterNode _viewRoot;
    private MoveCommand _pendingMoveCommand;
    private Node3D _singleplayerMoveTarget;
    private bool _hasSingleplayerTarget;
    private int _localOwnerId = 1;

    public Vector3 SingleplayerTarget => _singleplayerMoveTarget?.GlobalPosition ?? Vector3.Zero;
    public bool HasSingleplayerTarget => _hasSingleplayerTarget;

    public void BindCamera(CameraController camera) {
      _camera = camera;
    }

    public void BindViewRoot(EntityViewUpdaterNode viewRoot) {
      _viewRoot = viewRoot;
    }

    public void BindSingleplayerMoveTarget(Node3D target) {
      _singleplayerMoveTarget = target;
    }

    public void SetLocalOwnerId(int ownerId) {
      _localOwnerId = ownerId;
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
      _selectedViews.Clear();
      if (view != null) _selectedViews.Add(view);
    }

    public void HandleUnhandledInput(InputEvent @event) {
      if (_camera == null || @event is not InputEventMouseButton { Pressed: true } mouseButton) return;

      if (mouseButton.ButtonIndex == MouseButton.Left) {
        SelectNearestOwnedView(mouseButton.Position);
        return;
      }

      if (mouseButton.ButtonIndex != MouseButton.Right) return;

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
        UnitIdCount = 0,
      };

      for (int i = 0; i < _selectedViews.Count && command.UnitIdCount < MaxSelectedUnitIds; i++) {
        if (!TryGetUnitId(_selectedViews[i], out int unitId)) continue;
        command.SetUnitId(command.UnitIdCount, unitId);
        command.UnitIdCount++;
      }

      _pendingMoveCommand = command;
    }

    private void SelectNearestOwnedView(Vector2 screenPosition) {
      _selectedViews.Clear();
      if (_viewRoot == null || _camera == null) return;

      EntityViewNode best = null;
      float bestDistSqr = 22f * 22f;

      foreach (Node child in _viewRoot.GetChildren()) {
        if (child is not EntityViewNode view) continue;
        if (!view.OwnerMatches(_localOwnerId)) continue;

        Vector2 screen = _camera.UnprojectPosition(view.GlobalPosition);
        float distSqr = screen.DistanceSquaredTo(screenPosition);
        if (distSqr >= bestDistSqr) continue;

        best = view;
        bestDistSqr = distSqr;
      }

      if (best != null) _selectedViews.Add(best);
    }

    private static bool TryGetUnitId(EntityViewNode view, out int unitId) {
      unitId = 0;
      var frame = view.Engine?.PredictedFrame.Frame;
      if (frame == null || !view.EntityRef.IsValid || !frame.Has<Unit>(view.EntityRef)) return false;

      unitId = frame.GetReadOnly<Unit>(view.EntityRef).UnitId;
      return true;
    }

    public void Dispose() {
      _selectedViews.Clear();
      _pendingMoveCommand = null;
      _camera = null;
      _viewRoot = null;
      _singleplayerMoveTarget = null;
    }
  }
}
