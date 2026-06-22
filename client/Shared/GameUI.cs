using global::Godot;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Network;
using Meesles.Avalon.Sim.Models;

namespace Meesles.Avalon {
  public partial class GameUI : CanvasLayer, IViewHud {
    [Export] public float FocusRingRadiusPx { get; set; } = 52.0f;
    [Export] public float FocusRingWidthPx { get; set; } = 2.5f;
    [Export] public Color FocusRingColor { get; set; } = new Color(0.88f, 0.72f, 0.22f, 0.92f);

    private Label _timerLabel;
    private Label _focusTargetLabel;
    private Control _tabUI;
    private Label _scoreboardScoreLabel;
    private ColorRect _healthBar;
    private ColorRect _healthBarFill;
    private Label _healthBarLabel;
    private Control _selectionRectangle;
    private Panel _resultPanel;
    private Label _resultLabel;

    public override void _Ready() {
      ProcessMode = ProcessModeEnum.Always;
      SetProcessInput(true);

      _timerLabel = GetNode<Label>("DefaultUI/Timer");
      _focusTargetLabel = GetNode<Label>("DefaultUI/Focus");
      _tabUI = GetNode<Control>("TabUI");
      _scoreboardScoreLabel = GetNode<Label>("TabUI/ScoreboardPanel/Header/ScoreLabel");
      _healthBar = GetNode<ColorRect>("DefaultUI/HealthBar");
      _healthBarFill = GetNode<ColorRect>("DefaultUI/HealthBar/HealthBarFill");
      _healthBarLabel = GetNode<Label>("DefaultUI/HealthBar/HealthBarLabel");
      _selectionRectangle = GetNode<Control>("DefaultUI/SelectionRectangle");
      _resultPanel = GetNodeOrNull<Panel>("DefaultUI/ResultPanel");
      _resultLabel = GetNodeOrNull<Label>("DefaultUI/ResultPanel/ResultLabel");

      SetSelectionRectangle(null);
      if (_resultPanel != null) _resultPanel.Visible = false;
      SetupTabUI();
    }

    public override void _Input(InputEvent @event) {
      if (@event is InputEventKey key && key.Keycode == Key.Tab && !key.Echo) {
        if (_tabUI != null) _tabUI.Visible = key.Pressed;
        GetViewport().SetInputAsHandled();
      }
    }

    public void SyncFromFrame(Frame frame) {
      int p1 = 0, p2 = 0;
      var playerFilter = frame.Filter<Player>();
      while (playerFilter.Next(out var entity)) {
        ref readonly var player = ref frame.GetReadOnly<Player>(entity);
        if (player.PlayerId <= 1) p1 = player.Score;
        else if (player.PlayerId == 2) p2 = player.Score;
      }

      if (_scoreboardScoreLabel != null)
        _scoreboardScoreLabel.Text = $"{p1} / {p2}";

      double elapsed = (frame.Tick * (double)frame.DeltaTimeMs) / 1000.0;
      SetTimerText(FormatMatchTime((int)elapsed));
    }

    public void SetPhase(SessionPhase phase) {
      // Extend if you need phase-specific UI changes in the game view
    }

    public void SetCountdownRemaining(double seconds) {
      if (seconds < 0) seconds = 0;
      SetTimerText($"{seconds:0.0}s");
    }

    public void SetStartDelayRemaining(double seconds) {
      if (seconds < 0) seconds = 0;
      SetTimerText($"Start: {seconds:0.0}s");
    }

    public void SetLocalPlayerId(int? playerId) {
      // Show player info if needed
    }

    public void SetLocalReady(bool ready) {
      // No-op in game view; used by lobby flow
    }

    public void SetMultiplayerMode() {
      if (_scoreboardScoreLabel != null) _scoreboardScoreLabel.Text = "0 / 0";
      SetTimerText("0:00");
      if (_resultPanel != null) _resultPanel.Visible = false;
    }

    public void ShowResult(string text) {
      if (_resultPanel != null) _resultPanel.Visible = true;
      if (_resultLabel != null) _resultLabel.Text = text;
    }

    public void HideResult() {
      if (_resultPanel != null) _resultPanel.Visible = false;
    }

    public void SetFocusTargetLabel(string text) {
      if (_focusTargetLabel != null) _focusTargetLabel.Text = text;
    }

    public void SetPlayerHealth(float current, float maximum) {
      if (_healthBar == null || _healthBarFill == null) return;
      float ratio = maximum <= 0f ? 1f : Mathf.Clamp(current / maximum, 0f, 1f);
      _healthBarFill.Size = new Vector2(_healthBar.Size.X * ratio, _healthBar.Size.Y);
      if (_healthBarLabel != null)
        _healthBarLabel.Text = $"HP {(int)current} / {(int)maximum}";
    }

    public void SetSelectionRectangle(Rect2? rectangle) {
      if (_selectionRectangle == null) return;
      if (rectangle == null) {
        _selectionRectangle.Visible = false;
        return;
      }

      _selectionRectangle.Visible = true;
      _selectionRectangle.Position = rectangle.Value.Position;
      _selectionRectangle.Size = rectangle.Value.Size;
    }

    private void SetupTabUI() {
      if (_tabUI == null) return;
      _tabUI.TopLevel = true;
      _tabUI.Visible = false;
      UpdateTabUISize(_tabUI);
      GetViewport().SizeChanged += () => UpdateTabUISize(_tabUI);
    }

    private void UpdateTabUISize(Control tabUI) {
      var viewport = GetViewport();
      if (viewport == null) return;
      tabUI.Size = viewport.GetVisibleRect().Size;
      tabUI.Position = Vector2.Zero;
    }

    private void SetTimerText(string text) {
      if (_timerLabel != null && _timerLabel.Text != text)
        _timerLabel.Text = text;
    }

    private static string FormatMatchTime(int seconds) {
      int m = seconds / 60;
      int s = seconds % 60;
      return $"{m}:{s:D2}";
    }
  }
}
