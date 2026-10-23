#if TOOLS
using System;
using System.IO;
using System.Linq;
using Godot;
using Meesles.Avalon.Sim;
using Meesles.Avalon.Sim.Assets;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Client.Editor;

[Tool]
public partial class VisionMapPlugin : EditorPlugin {
  private const string Menu = "Avalon: Vision Map";
  private VBoxContainer _dock;
  private Label _status;
  private SpinBox _longEdge;
  private SpinBox _range;
  private SpinBox _height;
  private CheckBox _xray;
  private Node3D _overlay;
  private MeshInstance3D _boundaries;
  private MeshInstance3D _preview;
  private MapLayoutAsset _map;
  private Vector3? _probe;
  private string _path;
  private DateTime _modified;
  private double _elapsed;

  public override void _EnterTree() {
    AddToolMenuItem(Menu, Callable.From(ToggleActive));
    SetInputEventForwardingAlwaysEnabled();
    SceneChanged += OnSceneChanged;
  }

  public override void _ExitTree() {
    SceneChanged -= OnSceneChanged;
    RemoveToolMenuItem(Menu);
    Close();
  }

  public void ToggleActive() {
    if (_dock != null) {
      Close();
      return;
    }
    _dock = new VBoxContainer { Name = "Vision Map", CustomMinimumSize = new Vector2(260, 0) };
    _dock.AddChild(new Label { Text = "Saved vision map", TooltipText = "Reads the exported MapLayout.bytes used by the simulation." });
    _dock.AddChild(new Label { Text = "Cyan: sight edges · Red: long edges" });
    _dock.AddChild(new Label { Text = "Shift + click ground to place a probe.\nProbe samples the simulation's sight rules." });
    _longEdge = AddNumber("Highlight edges longer than", 10, 1, 100, 1);
    _range = AddNumber("Probe range", 18, 1, 100, 1);
    _height = AddNumber("Overlay height", 0.6, -10, 50, 0.1);
    _xray = new CheckBox { Text = "Show through geometry", ButtonPressed = true };
    _xray.Toggled += _ => Rebuild();
    _dock.AddChild(_xray);
    var reload = new Button { Text = "Reload saved bake" };
    reload.Pressed += ReloadData;
    _dock.AddChild(reload);
    var clear = new Button { Text = "Clear probe" };
    clear.Pressed += () => { _probe = null; Rebuild(); };
    _dock.AddChild(clear);
    _status = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
    _dock.AddChild(_status);
#pragma warning disable CS0618
    AddControlToDock(DockSlot.RightUl, _dock);
#pragma warning restore CS0618
    if (_dock.GetParent() is TabContainer tabs) tabs.CurrentTab = tabs.GetTabIdxFromControl(_dock);
    ReloadData();
  }

  private SpinBox AddNumber(string title, double value, double min, double max, double step) {
    _dock.AddChild(new Label { Text = title });
    var control = new SpinBox { MinValue = min, MaxValue = max, Step = step, Value = value };
    control.ValueChanged += _ => { if (_status != null) Rebuild(); };
    _dock.AddChild(control);
    return control;
  }

  private void Close() {
    DetachOverlay();
    if (_dock != null) {
#pragma warning disable CS0618
      RemoveControlFromDocks(_dock);
#pragma warning restore CS0618
      _dock.QueueFree();
    }
    _dock = null;
    _status = null;
    _map = null;
    _probe = null;
  }

  private void DetachOverlay() {
    if (GodotObject.IsInstanceValid(_overlay)) {
      _overlay.GetParent()?.RemoveChild(_overlay);
      _overlay.QueueFree();
    }
    _overlay = null;
    _boundaries = null;
    _preview = null;
  }

  private void OnSceneChanged(Node root) {
    if (_dock == null) return;
    _probe = null;
    ReloadData();
  }

  public override void _Process(double delta) {
    if (_dock == null || string.IsNullOrEmpty(_path)) return;
    _elapsed += delta;
    if (_elapsed < 1) return;
    _elapsed = 0;
    if (File.GetLastWriteTimeUtc(_path) != _modified) ReloadData();
  }

  public void ReloadData() {
    if (_dock == null) return;
    DetachOverlay();
    _map = null;
    _path = null;
    var root = EditorInterface.Singleton.GetEditedSceneRoot();
    if (root == null || string.IsNullOrEmpty(root.SceneFilePath)) {
      _status.Text = "Open a saved world scene first.";
      return;
    }
    var name = root.SceneFilePath.GetFile().GetBaseName();
    _path = ProjectSettings.GlobalizePath($"res://Sim/Data/MapLayout{(name == "World" ? "" : "_" + name)}.bytes");
    _modified = File.GetLastWriteTimeUtc(_path);
    try {
      WarmupRegistry.RunAll();
      _map = DataAssetReader.LoadMixedCollectionFromBytes(_path).OfType<MapLayoutAsset>().Single();
      if (_map.MapName != name || _map.SightEdges == null || _map.SightEdges.Length % 2 != 0 ||
          _map.VisionSize.x <= FP64.Zero || _map.VisionSize.y <= FP64.Zero)
        throw new InvalidDataException("Invalid vision map or scene name mismatch.");
      _overlay = new Node3D { Name = "VisionMapPreview" };
      root.AddChild(_overlay, false, InternalMode.Back);
      _overlay.TopLevel = true;
      _overlay.GlobalTransform = Transform3D.Identity;
      _boundaries = new MeshInstance3D { CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
      _preview = new MeshInstance3D { CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
      _overlay.AddChild(_boundaries);
      _overlay.AddChild(_preview);
      Rebuild();
    }
    catch (Exception ex) {
      _map = null;
      _status.Text = $"No usable bake for {name}.\nRun just export-scene-data.\n{ex.Message}";
    }
  }

  public override int _Forward3DGuiInput(Camera3D camera, InputEvent @event) {
    if (_dock == null || _map == null || @event is not InputEventMouseButton {
      ButtonIndex: MouseButton.Left, Pressed: true, ShiftPressed: true
    } click) return 0;
    var origin = camera.ProjectRayOrigin(click.Position);
    var direction = camera.ProjectRayNormal(click.Position);
    if (Mathf.Abs(direction.Y) < 0.0001f) return 0;
    var distance = -origin.Y / direction.Y;
    if (distance < 0) return 0;
    PlaceProbe(origin + direction * distance);
    return 1;
  }

  public void PlaceProbe(Vector3 position) {
    _probe = position;
    Rebuild();
  }

  private StandardMaterial3D Material(bool transparent) => new() {
    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
    NoDepthTest = _xray.ButtonPressed,
    VertexColorUseAsAlbedo = true,
    Transparency = transparent ? BaseMaterial3D.TransparencyEnum.Alpha : BaseMaterial3D.TransparencyEnum.Disabled,
    CullMode = BaseMaterial3D.CullModeEnum.Disabled
  };

  private void Rebuild() {
    if (_map == null || !GodotObject.IsInstanceValid(_overlay)) return;
    var y = (float)_height.Value;
    var lines = new ImmediateMesh();
    var hasLines = _map.SightEdges.Length > 0 || _probe.HasValue;
    if (hasLines) lines.SurfaceBegin(Mesh.PrimitiveType.Lines, Material(false));
    var count = 0;
    var longest = 0f;
    for (var i = 0; i < _map.SightEdges.Length; i += 2) {
      var a = _map.SightEdges[i];
      var b = _map.SightEdges[i + 1];
      var length = new Vector2((b.x - a.x).ToFloat(), (b.y - a.y).ToFloat()).Length();
      longest = Mathf.Max(longest, length);
      var highlight = length >= _longEdge.Value;
      if (highlight) count++;
      lines.SurfaceSetColor(highlight ? Colors.Red : Colors.Cyan);
      lines.SurfaceAddVertex(new Vector3(a.x.ToFloat(), y, a.y.ToFloat()));
      lines.SurfaceAddVertex(new Vector3(b.x.ToFloat(), y, b.y.ToFloat()));
    }
    if (_probe is { } probe) {
      lines.SurfaceSetColor(Colors.Yellow);
      lines.SurfaceAddVertex(new Vector3(probe.X - 0.5f, y + 0.05f, probe.Z));
      lines.SurfaceAddVertex(new Vector3(probe.X + 0.5f, y + 0.05f, probe.Z));
      lines.SurfaceAddVertex(new Vector3(probe.X, y + 0.05f, probe.Z - 0.5f));
      lines.SurfaceAddVertex(new Vector3(probe.X, y + 0.05f, probe.Z + 0.5f));
    }
    if (hasLines) lines.SurfaceEnd();
    _boundaries.Mesh = lines;
    _preview.Mesh = null;
    _status.Text = $"{_map.MapName} · last export {_modified.ToLocalTime():HH:mm:ss}\n" +
      $"{_map.SightEdges.Length / 2} edges · longest {longest:0.0}\n{count} long edges (review hints)\nScene edits require a new export.";
    if (_probe.HasValue) BuildProbe(y);
  }

  private void BuildProbe(float y) {
    var origin = new FPVector2(FP64.FromDouble(_probe.Value.X), FP64.FromDouble(_probe.Value.Z));
    var range = FP64.FromDouble(_range.Value);
    var walkable = _map.SightGeometry.IsWalkable(origin);
    var dx = _map.VisionSize.x / FP64.FromInt(128);
    var dz = _map.VisionSize.y / FP64.FromInt(128);
    var half = FP64.One / FP64.FromInt(2);
    var mesh = new ImmediateMesh();
    var started = false;
    for (var z = 0; z < 128; z++)
      for (var x = 0; x < 128; x++) {
        var point = _map.VisionOrigin + new FPVector2((FP64.FromInt(x) + half) * dx, (FP64.FromInt(z) + half) * dz);
        if (!Visibility.CanSee(_map, origin, point, range, FP64.Zero, sourceWalkable: walkable)) continue;
        if (!started) {
          mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles, Material(true));
          mesh.SurfaceSetColor(new Color(0.1f, 0.8f, 1f, 0.3f));
          started = true;
        }
        var a = new Vector3((point.x - dx * half).ToFloat(), y - 0.02f, (point.y - dz * half).ToFloat());
        var b = a + new Vector3(dx.ToFloat(), 0, 0);
        var c = a + new Vector3(dx.ToFloat(), 0, dz.ToFloat());
        var d = a + new Vector3(0, 0, dz.ToFloat());
        mesh.SurfaceAddVertex(a); mesh.SurfaceAddVertex(b); mesh.SurfaceAddVertex(c);
        mesh.SurfaceAddVertex(a); mesh.SurfaceAddVertex(c); mesh.SurfaceAddVertex(d);
      }
    if (started) mesh.SurfaceEnd();
    _preview.Mesh = mesh;
    _status.Text += $"\nProbe ({origin.x.ToFloat():0.0}, {origin.y.ToFloat():0.0}) · " +
      (walkable ? "open ground" : "inside blocker / outside map") + "\n128 × 128 samples · zero unit radius";
  }
}
#endif
