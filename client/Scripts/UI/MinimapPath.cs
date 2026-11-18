using Godot;
using Meesles.Avalon.Sim;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.Deterministic.Navigation;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Client;

[Tool]
public partial class MinimapPath : Control {
  [Export] public Color PathColor { get; set; } = new(1f, 0.15f, 0.1f, 0.8f);
  [Export] public float PathWidth { get; set; } = 1f;
  private readonly int[] _corridor = new int[NavAgentComponent.MAX_CORRIDOR];
  private readonly Vector3[] _points = new Vector3[FPNavMeshFunnel.MAX_WAYPOINTS + 1];
  private FPNavMeshFunnel _funnel;
  private int _pointCount;
  public MinimapBounds Bounds { get; set; }

  public override void _Ready() => Resized += QueueRedraw;

  public void BindNavigation(FPNavMesh mesh) {
    _funnel = new FPNavMeshFunnel(mesh, new FPNavMeshQuery(mesh, null), null);
  }

  public unsafe void Sync(Frame frame, int? playerId) {
    _pointCount = 0;
    QueueRedraw();
    if (_funnel == null || !playerId.HasValue ||
        !UnitLookup.TryGetPlayerHero(ref frame, playerId.Value, out var hero) ||
        frame.Has<PendingRespawn>(hero) || !frame.Has<NavAgentComponent>(hero)) return;
    var nav = frame.GetReadOnly<NavAgentComponent>(hero);
    if (!nav.HasNavDestination || nav.Status == (byte)FPNavAgentStatus.Arrived ||
        nav.Status == (byte)FPNavAgentStatus.Idle || nav.Status == (byte)FPNavAgentStatus.PathFailed) return;
    if (!nav.HasPath || !nav.PathIsValid || nav.CorridorLength <= 0) return;
    var length = System.Math.Min(nav.CorridorLength, _corridor.Length);
    for (var i = 0; i < length; i++) _corridor[i] = nav.Corridor[i];
    var count = _funnel.FindCorners(_corridor, length, nav.Position, nav.PathTarget, FPNavMeshFunnel.MAX_WAYPOINTS);
    if (count == 0) return;
    _points[_pointCount++] = ToWorld(nav.Position);
    for (var i = 0; i < count; i++) _points[_pointCount++] = ToWorld(_funnel.Corners[i]);
  }

  private static Vector3 ToWorld(FPVector3 point) => new(point.x.ToFloat(), point.y.ToFloat(), point.z.ToFloat());

  public override void _Draw() {
    if (Engine.IsEditorHint()) {
      var start = Size * new Vector2(0.2f, 0.8f);
      var bend = Size * new Vector2(0.4f, 0.5f);
      var end = Size * new Vector2(0.7f, 0.3f);
      DrawLine(start, bend, PathColor, PathWidth, true);
      DrawLine(bend, end, PathColor, PathWidth, true);
      return;
    }
    if (!GodotObject.IsInstanceValid(Bounds) || Bounds.Ground == null) return;
    for (var i = 1; i < _pointCount; i++)
      DrawLine(Bounds.WorldToUv(_points[i - 1]) * Size, Bounds.WorldToUv(_points[i]) * Size, PathColor, PathWidth, true);
  }
}
