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

  private readonly record struct Sample(ulong Usec, ulong Frame, string Kind, double X, double Y,
    double Width, double Height, int Tick, double DurationMs);

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
  private void AfterDraw() => Add("draw_end");

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
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_output))!);
    using var writer = new StreamWriter(_output);
    writer.WriteLine($"# utc_origin={_utcOrigin:O}; clock=Godot.Time.GetTicksUsec; origin_usec={_origin}");
    writer.WriteLine("usec,frame,kind,x,y,width,height,tick,duration_ms");
    foreach (var s in _samples)
      writer.WriteLine(FormattableString.Invariant($"{s.Usec},{s.Frame},{s.Kind},{s.X},{s.Y},{s.Width},{s.Height},{s.Tick},{s.DurationMs:F3}"));
    GD.Print($"[InputLatency] Saved {_samples.Count} samples to {_output}");
  }
}
