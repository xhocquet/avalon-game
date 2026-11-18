using System;
using System.Collections.Generic;
using System.IO;
using Godot;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Godot;

namespace Meesles.Avalon;

public partial class InputLatencyProbe : Node {
  public static InputLatencyProbe Active { get; private set; }

  private readonly List<Sample> _samples = new(65536);
  private GodotSessionDriver _driver;
  private string _output;
  private ulong _origin;
  private DateTime _utcOrigin;
  private ulong _sessionStart;
  private int _tickStart;
  private bool _recording;
  private List<RenderSample> _renderSamples;
  private readonly List<(Viewport View, Rid Rid, string Name)> _renderViewports = new();

  private readonly record struct Sample(ulong Usec, ulong Frame, string Kind, double X, double Y,
    double Width, double Height, int Tick, double DurationMs);
  private readonly record struct RenderSample(ulong Usec, ulong Frame, int DrawnFrame, string Viewport,
    double CpuMs, double GpuMs, double SetupCpuMs, int DrawCalls, int Primitives, int Objects);

  public static void Attach(Node parent, GodotSessionDriver driver) {
    foreach (var arg in OS.GetCmdlineUserArgs()) {
      if (!arg.StartsWith("--input-latency-output=")) continue;
      var probe = new InputLatencyProbe { _output = arg["--input-latency-output=".Length..], _driver = driver };
      parent.AddChild(probe);
      return;
    }
  }

  public override void _Ready() {
    _origin = Time.GetTicksUsec();
    _utcOrigin = DateTime.UtcNow;
    _recording = true;
    Active = this;
    ProcessPriority = -100000;
    _driver.PreSessionUpdate += BeginSession;
    _driver.PostSessionUpdate += EndSession;
    RenderingServer.FramePreDraw += BeforeDraw;
    RenderingServer.FramePostDraw += AfterDraw;
    if (Array.IndexOf(OS.GetCmdlineUserArgs(), "--render-timings") >= 0 && DisplayServer.GetName() != "headless") {
      _renderSamples = new List<RenderSample>(8192);
      RegisterViewport(GetViewport(), "main");
      var children = GetParent().FindChildren("*", "SubViewport", true, false);
      foreach (var child in children)
        if (child is Viewport viewport) RegisterViewport(viewport, viewport.GetPath());
      GetTree().NodeAdded += OnNodeAdded;
      GD.Print($"[RenderTiming] Enabled for {_renderViewports.Count} viewports; debugger_active={EngineDebugger.IsActive()}");
    }
    GD.Print($"[InputLatency] Recording manual input to {_output}");
  }

  public override void _Process(double delta) => Add("frame_start");

  public void RecordInput(InputEvent input) {
    if (input is InputEventKey { Pressed: true, Echo: false, Keycode: Key.F8 })
      Add("marker");
    else if (input is InputEventMouseMotion motion)
      Add("mouse_motion", motion.Position.X, motion.Position.Y);
    else if (input is InputEventMouseButton button)
      Add(button.Pressed ? "mouse_down" : "mouse_up", button.Position.X, button.Position.Y,
        tick: (int)button.ButtonIndex);
  }

  public void RecordRectangle(Rect2? rectangle) {
    if (rectangle is { } rect)
      Add("rectangle", rect.Position.X, rect.Position.Y, rect.Size.X, rect.Size.Y);
    else
      Add("rectangle_hidden");
  }

  private void BeginSession(KlothoSession session, float delta) {
    _sessionStart = Time.GetTicksUsec();
    _tickStart = session.Engine?.CurrentTick ?? -1;
    Add("session_start", tick: _tickStart);
  }

  private void EndSession(KlothoSession session, float delta) =>
    Add("session_end", x: _tickStart, tick: session.Engine?.CurrentTick ?? -1,
      durationMs: (Time.GetTicksUsec() - _sessionStart) / 1000.0);

  private void BeforeDraw() => Add("draw_start");
  private void AfterDraw() {
    Add("draw_end");
    if (_renderSamples == null || !_recording) return;
    if (_renderSamples.Count >= 1000000) { _recording = false; return; }
    var now = Time.GetTicksUsec() - _origin;
    var frame = Engine.GetProcessFrames();
    var drawn = Engine.GetFramesDrawn();
    var setup = RenderingServer.GetFrameSetupTimeCpu();
    for (var i = _renderViewports.Count - 1; i >= 0; i--) {
      var entry = _renderViewports[i];
      if (!GodotObject.IsInstanceValid(entry.View) || !entry.View.IsInsideTree()) {
        _renderViewports.RemoveAt(i);
        continue;
      }
      _renderSamples.Add(new RenderSample(now, frame, drawn, entry.Name,
        RenderingServer.ViewportGetMeasuredRenderTimeCpu(entry.Rid),
        RenderingServer.ViewportGetMeasuredRenderTimeGpu(entry.Rid), setup,
        RenderingServer.ViewportGetRenderInfo(entry.Rid, RenderingServer.ViewportRenderInfoType.Visible,
          RenderingServer.ViewportRenderInfo.DrawCallsInFrame),
        RenderingServer.ViewportGetRenderInfo(entry.Rid, RenderingServer.ViewportRenderInfoType.Visible,
          RenderingServer.ViewportRenderInfo.PrimitivesInFrame),
        RenderingServer.ViewportGetRenderInfo(entry.Rid, RenderingServer.ViewportRenderInfoType.Visible,
          RenderingServer.ViewportRenderInfo.ObjectsInFrame)));
    }
  }

  private void OnNodeAdded(Node node) {
    if (node is SubViewport viewport && GetParent().IsAncestorOf(node))
      RegisterViewport(viewport, viewport.GetPath());
  }

  private void RegisterViewport(Viewport viewport, string name) {
    var rid = viewport.GetViewportRid();
    foreach (var entry in _renderViewports)
      if (entry.Rid == rid) return;
    RenderingServer.ViewportSetMeasureRenderTime(rid, true);
    _renderViewports.Add((viewport, rid, name));
  }

  private void Add(string kind, double x = 0, double y = 0, double width = 0, double height = 0,
    int tick = -1, double durationMs = 0) {
    if (!_recording) return;
    if (_samples.Count >= 1000000) { _recording = false; return; }
    _samples.Add(new Sample(Time.GetTicksUsec() - _origin, Engine.GetProcessFrames(), kind,
      x, y, width, height, tick, durationMs));
  }

  public override void _ExitTree() {
    _recording = false;
    if (Active == this) Active = null;
    _driver.PreSessionUpdate -= BeginSession;
    _driver.PostSessionUpdate -= EndSession;
    RenderingServer.FramePreDraw -= BeforeDraw;
    RenderingServer.FramePostDraw -= AfterDraw;
    if (_renderSamples != null) {
      GetTree().NodeAdded -= OnNodeAdded;
      foreach (var entry in _renderViewports)
        if (GodotObject.IsInstanceValid(entry.View) && entry.View.IsInsideTree())
          RenderingServer.ViewportSetMeasureRenderTime(entry.Rid, false);
      WriteRenderSamples();
    }
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_output))!);
    using var writer = new StreamWriter(_output);
    writer.WriteLine($"# utc_origin={_utcOrigin:O}; clock=Godot.Time.GetTicksUsec; origin_usec={_origin}");
    writer.WriteLine("usec,frame,kind,x,y,width,height,tick,duration_ms");
    foreach (var s in _samples)
      writer.WriteLine(FormattableString.Invariant($"{s.Usec},{s.Frame},{s.Kind},{s.X},{s.Y},{s.Width},{s.Height},{s.Tick},{s.DurationMs:F3}"));
    GD.Print($"[InputLatency] Saved {_samples.Count} samples to {_output}");
  }

  private void WriteRenderSamples() {
    var output = _output.EndsWith(".latency.csv", StringComparison.Ordinal)
      ? _output[..^".latency.csv".Length] + ".render.csv" : _output + ".render.csv";
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
    using var writer = new StreamWriter(output);
    writer.WriteLine($"# utc_origin={_utcOrigin:O}; origin_usec={_origin}; alignment=latest_available; GPU measurements may lag observation");
    writer.WriteLine("usec,observed_process_frame,observed_drawn_frame,viewport,cpu_ms,gpu_ms,setup_cpu_ms,draw_calls,primitives,objects");
    foreach (var s in _renderSamples) {
      var name = "\"" + s.Viewport.Replace("\"", "\"\"") + "\"";
      writer.WriteLine(FormattableString.Invariant($"{s.Usec},{s.Frame},{s.DrawnFrame},{name},{s.CpuMs:F6},{s.GpuMs:F6},{s.SetupCpuMs:F6},{s.DrawCalls},{s.Primitives},{s.Objects}"));
    }
    GD.Print($"[RenderTiming] Saved {_renderSamples.Count} viewport samples to {output}");
  }
}
