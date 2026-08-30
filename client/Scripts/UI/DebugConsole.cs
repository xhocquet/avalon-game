using System;
using System.Collections.Generic;
using System.Globalization;
using Godot;
using Meesles.Avalon.Sim;
using Meesles.Avalon.Sim.Commands;
using xpTURN.Klotho.Deterministic.Math;
using ICommand = xpTURN.Klotho.Core.ICommand;
using IKlothoEngine = xpTURN.Klotho.Core.IKlothoEngine;

namespace Meesles.Avalon;

// Playground command line. Everything it can do goes out as a DebugCommand or SetCheatCommand and is
// applied by the sim, so a console action rolls back and resimulates like any other input rather than
// being poked into the view.
//
// Bound by GameNode when the game scene carries one; scenes without it behave as before.
public partial class DebugConsole : CanvasLayer {
  private const int MaxHistory = 32;
  private const int MaxOutputLines = 200;

  private readonly List<string> _history = [];
  private readonly List<string> _lines = [];
  private CameraController _camera;
  private IKlothoEngine _engine;
  private int _historyCursor = -1;
  private LineEdit _input;
  private InputCapture _inputCapture;
  private RichTextLabel _output;
  private Control _panel;

  // Last ground point the cursor was over while it was not on the console. A button click puts the
  // pointer on the panel, so `spawn` and `tp` fired from one aim at wherever you were last looking
  // rather than at whatever sits behind the button.
  private Vector3 _lastWorldPoint;

  // Each button is the console line it runs, so a button and the typed command cannot drift apart.
  private static readonly (string Label, string Command)[] QuickActions = [
    ("Max Skills", "maxskills"),
    ("Refresh CDs", "cd"),
    ("+1000 Gold", "gold"),
    ("+5000 XP", "xp 5000"),
    ("Spawn Enemies", "spawn"),
    ("Clear Minions", "clear"),
    ("Heal", "heal"),
    ("Kill", "kill"),
    ("God", "god"),
    ("Free Shop", "freeshop")
  ];

  public bool IsOpen => Visible;

  public override void _Ready() {
    _panel = GetNode<Control>("Panel");
    _output = GetNode<RichTextLabel>("Panel/Layout/Output");
    _input = GetNode<LineEdit>("Panel/Layout/Input");
    _input.TextSubmitted += OnSubmitted;
    BuildQuickActions(GetNode<HFlowContainer>("Panel/Layout/Actions"));
    Visible = false;
    PrintHelp();
  }

  // FocusMode.None on every button: a click runs the command and leaves the caret in the prompt, so
  // the buttons and typing are not two modes.
  private void BuildQuickActions(HFlowContainer row) {
    foreach (var (label, command) in QuickActions) {
      var button = new Button { Text = label, FocusMode = Control.FocusModeEnum.None };
      var line = command;
      button.Pressed += () => {
        Print($"[color=#7fb2ff]> {line}[/color]");
        Run(line);
      };
      row.AddChild(button);
    }
  }

  public void Bind(InputCapture inputCapture, IKlothoEngine engine, CameraController camera) {
    _inputCapture = inputCapture;
    _engine = engine;
    _camera = camera;
    GD.Print("[DebugConsole] bound - F1 or ` to open");
  }

  // True when the event belongs to the console, which is also what keeps a keystroke typed into the
  // prompt from doubling as a gameplay hotkey - GameNode._Input runs ahead of UI focus handling.
  public bool HandleInput(InputEvent @event) {
    if (@event is InputEventKey { Echo: false, Pressed: true } key) {
      if (key.Keycode is Key.F1 or Key.Quoteleft) {
        Toggle();
        return Claim();
      }

      if (!Visible)
        return false;

      switch (key.Keycode) {
        case Key.Escape:
          Close();
          return Claim();
        case Key.Up:
          StepHistory(1);
          return Claim();
        case Key.Down:
          StepHistory(-1);
          return Claim();
      }
    }

    if (@event is InputEventMouseMotion motion) {
      TrackWorldPoint(motion.Position);
      return false; // Camera edge-pan keeps working with the console open
    }

    // Keys are swallowed whenever the console is open; clicks only when they land on it, so the world
    // behind stays selectable and `spawn`/`tp` can still be aimed.
    return Visible && (@event is InputEventKey || IsPointerOverPanel());
  }

  // Marks the event consumed for the whole tree, not just for InputCapture: without it the backtick
  // that opens the console is also typed into the prompt, and Escape would reach the game as well.
  private bool Claim() {
    GetViewport().SetInputAsHandled();
    return true;
  }

  private void TrackWorldPoint(Vector2 screenPosition) {
    if (_camera == null || IsPointerOverPanel())
      return;

    var ground = _camera.ScreenToGround(screenPosition);
    if (ground != null)
      _lastWorldPoint = ground.Value;
  }

  private bool IsPointerOverPanel() {
    return Visible && _panel != null && _panel.GetGlobalRect().HasPoint(_panel.GetGlobalMousePosition());
  }

  public void Toggle() {
    if (Visible) Close();
    else Open();
  }

  private void Open() {
    Visible = true;
    _historyCursor = -1;
    _input.Clear();
    _input.GrabFocus();
  }

  private void Close() {
    Visible = false;
    _input.ReleaseFocus();
  }

  private void OnSubmitted(string text) {
    _input.Clear();
    _input.GrabFocus();
    if (string.IsNullOrWhiteSpace(text))
      return;

    _historyCursor = -1;
    _history.Insert(0, text);
    if (_history.Count > MaxHistory)
      _history.RemoveAt(_history.Count - 1);

    Print($"[color=#7fb2ff]> {text}[/color]");
    Run(text.Trim());
  }

  private void StepHistory(int direction) {
    if (_history.Count == 0)
      return;

    _historyCursor = Mathf.Clamp(_historyCursor + direction, -1, _history.Count - 1);
    _input.Text = _historyCursor < 0 ? "" : _history[_historyCursor];
    _input.CaretColumn = _input.Text.Length;
  }

  private void Run(string line) {
    var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    var verb = parts[0].ToLowerInvariant();
    var arg = parts.Length > 1 ? parts[1] : null;
    var arg2 = parts.Length > 2 ? parts[2] : null;

    switch (verb) {
      case "help":
        PrintHelp();
        return;
      case "clearlog":
        _lines.Clear();
        Repaint();
        return;
      case "hero":
        SwitchHero(arg);
        return;
      case "gold":
        SendAction(DebugAction.AddGold, ParseInt(arg, 1000));
        return;
      case "xp":
        SendAction(DebugAction.AddExperience, ParseInt(arg, 1000));
        return;
      case "sp":
        SendAction(DebugAction.AddSkillPoints, ParseInt(arg, 1));
        return;
      case "maxskills":
        SendAction(DebugAction.MaxSkills, 0);
        return;
      case "cd":
        SendAction(DebugAction.RefreshCooldowns, 0);
        return;
      case "heal":
        SendAction(DebugAction.HealFull, 0);
        return;
      case "kill":
        SendAction(DebugAction.KillHero, 0);
        return;
      case "tp":
        SendAction(DebugAction.TeleportHero, 0, atCursor: true);
        return;
      case "spawn":
        SpawnMinions(arg, arg2);
        return;
      case "clear":
        SendAction(DebugAction.ClearMinions, ParseInt(arg, 0));
        return;
      case "god":
        SetCheat(CheatFlags.GodMode, arg);
        return;
      case "freeshop":
        SetCheat(CheatFlags.FreeShop, arg);
        return;
      case "cheats":
        PrintCheats();
        return;
      default:
        Print($"[color=#ff8080]unknown command: {verb}[/color]");
        return;
    }
  }

  // Printed on launch as well as on `help`: the whole surface is small enough to read at a glance,
  // which beats making the first thing anyone types be `help`.
  private void PrintHelp() {
    Print("""
          [color=#7fb2ff]Debug console[/color]  —  F1 or ` toggles, Esc closes, Up/Down walks history
            hero <name|id>   switch faction, respawns the hero
            gold [n]         grant gold (default 1000)
            xp [n]           grant experience (default 1000)
            sp [n]           grant skill points (default 1)
            maxskills        rank every skill to max
            cd               clear skill cooldowns
            heal / kill      restore, or zero HP into the respawn path
            tp               teleport the hero to the cursor
            spawn [team] [faction]   minions at the cursor (default: enemy team, their faction)
            clear [team]     remove minions (default: all teams)
            god / freeshop [on|off]   toggle a cheat, no argument flips it
            cheats           show active cheat flags
            clearlog         wipe this log
          """);
    Print($"  heroes: {string.Join(", ", HeroNames())}");
  }

  // `spawn`, `spawn 3`, `spawn 3 pickle`. The faction is optional and only decides which minion model
  // shows up - without one the sim picks whatever the target team plays.
  private void SpawnMinions(string teamArg, string factionArg) {
    var factionId = 0;
    if (factionArg != null) {
      if (!TryResolveFaction(factionArg, out var def)) {
        Print($"[color=#ff8080]no hero matching '{factionArg}'[/color]");
        return;
      }

      factionId = def.Id;
    }

    SendAction(DebugAction.SpawnMinions, ParseInt(teamArg, 0), atCursor: true, factionId: factionId);
  }

  private void SwitchHero(string arg) {
    if (arg == null) {
      Print($"heroes: {string.Join(", ", HeroNames())}");
      return;
    }

    if (!TryResolveFaction(arg, out var def)) {
      Print($"[color=#ff8080]no hero matching '{arg}'[/color]");
      return;
    }

    Print($"switching to {def.Name}");
    SendAction(DebugAction.SwitchFaction, def.Id);
  }

  // Accepts the faction id or any prefix of the display name with the spaces taken out, so `hero
  // pickle` and `hero 204` both land.
  private static bool TryResolveFaction(string arg, out FactionCatalog.FactionDef match) {
    if (int.TryParse(arg, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
      foreach (var def in FactionCatalog.FactionDefs)
        if (def.Id == id) {
          match = def;
          return true;
        }

    var needle = arg.ToLowerInvariant();
    foreach (var def in FactionCatalog.FactionDefs)
      if (def.Name.Replace(" ", "").ToLowerInvariant().StartsWith(needle, StringComparison.Ordinal)) {
        match = def;
        return true;
      }

    match = default;
    return false;
  }

  private static IEnumerable<string> HeroNames() {
    foreach (var def in FactionCatalog.FactionDefs)
      yield return def.Name.Replace(" ", "");
  }

  private void SendAction(DebugAction action, int param, bool atCursor = false, int factionId = 0) {
    var target = atCursor ? CursorGroundPoint() : Vector3.Zero;
    Send(new DebugCommand {
      Action = (int)action,
      Param = param,
      FactionId = factionId,
      TargetX = FP64.FromFloat(target.X),
      TargetZ = FP64.FromFloat(target.Z)
    });
  }

  private void SetCheat(CheatFlags flag, string arg) {
    var enabled = arg == null ? !IsCheatOn(flag) : arg is "1" or "on" or "true";
    Print($"{flag} {(enabled ? "on" : "off")}");
    Send(new SetCheatCommand { Flags = (int)flag, Enabled = enabled ? 1 : 0 });
  }

  private void PrintCheats() {
    var active = new List<string>();
    foreach (var flag in Enum.GetValues<CheatFlags>())
      if (flag != CheatFlags.None && IsCheatOn(flag))
        active.Add(flag.ToString());

    Print(active.Count == 0 ? "no cheats active" : string.Join(", ", active));
  }

  private bool IsCheatOn(CheatFlags flag) {
    var frame = _engine?.PredictedFrame.Frame;
    return frame != null && Cheats.IsEnabled(ref frame, _engine.LocalPlayerId, flag);
  }

  private static int ParseInt(string arg, int fallback) {
    return int.TryParse(arg, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
      ? value
      : fallback;
  }

  private Vector3 CursorGroundPoint() {
    if (_camera == null)
      return _lastWorldPoint;

    if (IsPointerOverPanel())
      return _lastWorldPoint;

    return _camera.ScreenToGround(_camera.GetViewport().GetMousePosition()) ?? _lastWorldPoint;
  }

  private void Send(ICommand command) {
    if (_inputCapture == null) {
      Print("[color=#ff8080]console not bound to a session[/color]");
      return;
    }

    _inputCapture.QueueDebugCommand(command);
  }

  private void Print(string text) {
    foreach (var line in text.Split('\n'))
      _lines.Add(line.TrimEnd());

    if (_lines.Count > MaxOutputLines)
      _lines.RemoveRange(0, _lines.Count - MaxOutputLines);

    Repaint();
  }

  private void Repaint() {
    if (_output == null)
      return;

    _output.Text = string.Join("\n", _lines);
    _output.ScrollToLine(_output.GetLineCount() - 1);
  }
}
