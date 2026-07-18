using System;
using System.Collections.Generic;
using Godot;
using Meesles.Avalon.Sim;
using Meesles.Avalon.Sim.Models;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Network;

namespace Meesles.Avalon;

public partial class GameUI : CanvasLayer, IViewHud {
  private readonly List<IDisposable> _eventSubscriptions = new();
  private Label _announceLabel;
  private Tween _announceTween;

  private SimEventHub _events;
  private Label _focusTargetLabel;
  private ColorRect _healthBar;
  private ColorRect _healthBarFill;
  private Label _healthBarLabel;
  private Label _goldLabel;
  private Label _resourcesLabel;
  private Label _strengthLabel;
  private int? _localPlayerId;
  private Label _resultLabel;
  private Panel _resultPanel;
  private Label _scoreboardScoreLabel;
  private Control _selectionRectangle;
  private Control _tabUi;

  private Label _timerLabel;
  private SubViewport _minimapViewport;
  private Camera3D _minimapCamera;
  private TextureRect _portraitTexture;
  private Label _portraitLabel;
  private Texture2D _portraitPlaceholder;
  [Export] public float FocusRingRadiusPx { get; set; } = 52.0f;
  [Export] public float FocusRingWidthPx { get; set; } = 2.5f;
  [Export] public Color FocusRingColor { get; set; } = new(0.88f, 0.72f, 0.22f, 0.92f);

  // Covers the World.tscn ground plane (100x100, centered at origin).
  [Export] public float MinimapOrthoSize { get; set; } = 110.0f;
  [Export] public float MinimapHeight { get; set; } = 60.0f;

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

    UpdateLocalPlayerHealth(frame);
    UpdateLocalPlayerInventory(frame);
    UpdateLocalPlayerStats(frame);

    var elapsed = frame.Tick * (double)frame.DeltaTimeMs / 1000.0;
    SetTimerText(FormatMatchTime((int)elapsed));
  }

  public void SetLocalPlayerId(int? playerId) {
    _localPlayerId = playerId is int id && id >= 0 ? id : null;
  }

  public void ShowResult(string text) {
    if (_resultPanel != null) _resultPanel.Visible = true;
    if (_resultLabel != null) _resultLabel.Text = text;
  }

  public void HideResult() {
    if (_resultPanel != null) _resultPanel.Visible = false;
  }

  public override void _Ready() {
    ProcessMode = ProcessModeEnum.Always;
    SetProcessInput(true);

    _timerLabel = GetNode<Label>("DefaultUI/Timer");
    _focusTargetLabel = GetNodeOrNull<Label>("DefaultUI/Focus");
    _tabUi = GetNode<Control>("TabUI");
    _scoreboardScoreLabel = GetNode<Label>("TabUI/ScoreboardPanel/Header/ScoreLabel");
    _healthBar = GetNode<ColorRect>("DefaultUI/BottomBar/MarginContainer/Panels/Vbox/HealthBar");
    _healthBarFill = GetNode<ColorRect>("DefaultUI/BottomBar/MarginContainer/Panels/Vbox/HealthBar/HealthBarFill");
    _healthBarLabel = GetNode<Label>("DefaultUI/BottomBar/MarginContainer/Panels/Vbox/HealthBar/HealthBarLabel");
    _goldLabel =
      GetNodeOrNull<Label>("DefaultUI/BottomBar/MarginContainer/Panels/Vbox/MainSection/MinionAndStatsPanel/GoldLabel");
    _resourcesLabel =
      GetNodeOrNull<Label>(
        "DefaultUI/BottomBar/MarginContainer/Panels/Vbox/MainSection/MinionAndStatsPanel/ResourcesLabel");
    _strengthLabel =
      GetNodeOrNull<Label>(
        "DefaultUI/BottomBar/MarginContainer/Panels/Vbox/MainSection/MinionAndStatsPanel/StrengthLabel");
    _selectionRectangle = GetNode<Control>("DefaultUI/SelectionRectangle");
    _resultPanel = GetNodeOrNull<Panel>("DefaultUI/ResultPanel");
    _resultLabel = GetNodeOrNull<Label>("DefaultUI/ResultPanel/ResultLabel");
    _minimapViewport =
      GetNodeOrNull<SubViewport>("DefaultUI/BottomBar/MarginContainer/Panels/MinimapContainer/MinimapViewport");
    _portraitTexture = GetNodeOrNull<TextureRect>(
      "DefaultUI/BottomBar/MarginContainer/Panels/Vbox/MainSection/HeroMarginPanel/VBox/PortraitTexture");
    _portraitLabel = GetNodeOrNull<Label>(
      "DefaultUI/BottomBar/MarginContainer/Panels/Vbox/MainSection/HeroMarginPanel/VBox/PortraitLabel");

    SetSelectionRectangle(null);
    if (_resultPanel != null) _resultPanel.Visible = false;
    SetupTabUi();
    SetupAnnouncement();
    SetupMinimap();
    SetFocusPortrait(null, null);
  }

  public override void _ExitTree() {
    UnbindSimEvents();
    _announceTween?.Kill();
  }

  // Called by the owning GameNode once a session's engine is attached. GameUI is one of many UI
  // listeners; each subscribes to just the events it renders and reacts in its own handler. The
  // hub delivers confirmed (verified) events exactly once, so these handlers never see rollback
  // flicker. Subscriptions are torn down in UnbindSimEvents / _ExitTree.
  public void BindSimEvents(SimEventHub hub) {
    UnbindSimEvents();
    _events = hub;
    if (hub == null) return;

    _eventSubscriptions.Add(hub.OnConfirmed<CrystalDestroyedEvent>(OnCrystalDestroyed));
    _eventSubscriptions.Add(hub.OnConfirmed<TurretDestroyedEvent>(OnTurretDestroyed));
    _eventSubscriptions.Add(hub.OnConfirmed<PlayerDiedEvent>(OnPlayerDied));
    _eventSubscriptions.Add(hub.OnConfirmed<PlayerRespawnedEvent>(OnPlayerRespawned));
  }

  private void UnbindSimEvents() {
    foreach (var sub in _eventSubscriptions)
      sub.Dispose();
    _eventSubscriptions.Clear();
    _events = null;
  }

  private void OnCrystalDestroyed(CrystalDestroyedEvent evt) {
    ShowAnnouncement("Crystal Destroyed!");
  }

  private void OnTurretDestroyed(TurretDestroyedEvent evt) {
    ShowAnnouncement("Turret Destroyed!");
  }

  private void OnPlayerDied(PlayerDiedEvent evt) {
    if (_localPlayerId is int id && evt.PlayerId == id)
      ShowAnnouncement("You were defeated");
  }

  private void OnPlayerRespawned(PlayerRespawnedEvent evt) {
    if (_localPlayerId is int id && evt.PlayerId == id)
      ShowAnnouncement("Respawned");
  }

  public override void _Input(InputEvent @event) {
    if (@event is InputEventKey key && key.Keycode == Key.Tab && !key.Echo) {
      if (_tabUi != null) _tabUi.Visible = key.Pressed;
      GetViewport().SetInputAsHandled();
    }
  }

  // HP is continuous state (damage, heals, respawn resets), so it is read from the frame every
  // tick rather than pushed via SimEventHub - polling stays correct through rollback and never
  // misses an intermediate value. The local hero entity persists through death (Health.Current
  // drops to 0) and respawn (refilled to Max), so this reflects the dead/respawning window too.
  private void UpdateLocalPlayerHealth(Frame frame) {
    if (_localPlayerId is not int localId) return;

    var filter = frame.Filter<Hero, Health>();
    while (filter.Next(out var entity)) {
      ref readonly var hero = ref frame.GetReadOnly<Hero>(entity);
      if (hero.PlayerId != localId) continue;

      ref readonly var health = ref frame.GetReadOnly<Health>(entity);
      SetPlayerHealth(health.Current, health.Max);
      return;
    }
  }

  private void UpdateLocalPlayerInventory(Frame frame) {
    if (_localPlayerId is not int localId) return;

    var filter = frame.Filter<Hero, Inventory>();
    while (filter.Next(out var entity)) {
      ref readonly var hero = ref frame.GetReadOnly<Hero>(entity);
      if (hero.PlayerId != localId) continue;

      ref readonly var inventory = ref frame.GetReadOnly<Inventory>(entity);
      SetGoldText(inventory.Gold);
      SetResourcesText(inventory.Resources);
      return;
    }
  }

  private void SetGoldText(int gold) {
    if (_goldLabel != null)
      _goldLabel.Text = $"Gold: {gold}";
  }

  private void SetResourcesText(int resources) {
    if (_resourcesLabel != null)
      _resourcesLabel.Text = $"Resources: {resources}";
  }

  private void UpdateLocalPlayerStats(Frame frame) {
    if (_localPlayerId is not int localId) return;

    var filter = frame.Filter<Hero, Stats>();
    while (filter.Next(out var entity)) {
      ref readonly var hero = ref frame.GetReadOnly<Hero>(entity);
      if (hero.PlayerId != localId) continue;

      ref readonly var stats = ref frame.GetReadOnly<Stats>(entity);
      SetStrengthText(stats.Strength);
      return;
    }
  }

  private void SetStrengthText(int strength) {
    if (_strengthLabel != null)
      _strengthLabel.Text = $"Strength: {strength}";
  }

  public void SetPhase(SessionPhase phase) {
    // Extend if you need phase-specific UI changes in the game view
  }

  public void SetCountdownRemaining(double seconds) {
    if (seconds < 0) seconds = 0;
    SetTimerText($"{seconds:0.0}s");
  }

  public void SetLocalReady(bool ready) {
    // No-op in game view; used by lobby flow
  }

  public void SetMultiplayerMode() {
    if (_scoreboardScoreLabel != null) _scoreboardScoreLabel.Text = "0 / 0";
    SetTimerText("0:00");
    if (_resultPanel != null) _resultPanel.Visible = false;
  }

  public void SetFocusTargetLabel(string text) {
    if (_focusTargetLabel != null) _focusTargetLabel.Text = text;
  }

  public void SetPlayerHealth(float current, float maximum) {
    if (_healthBar == null || _healthBarFill == null) return;
    var ratio = maximum <= 0f ? 1f : Mathf.Clamp(current / maximum, 0f, 1f);
    _healthBarFill.Size = new Vector2(_healthBar.Size.X * ratio, _healthBar.Size.Y);
    if (_healthBarLabel != null)
      _healthBarLabel.Text = $"HP {(int)current} / {(int)maximum}";
  }

  // Driven by InputCapture's selection system - shows the portrait of the currently selected
  // hero. All heroes of a faction currently share one portrait, so this resolves per-faction
  // rather than per-hero; revisit once each hero gets its own art. When nothing is mapped we
  // fall back to a TODO placeholder so the portrait slot never renders blank.
  public void SetFocusPortrait(Texture2D texture, string label) {
    if (_portraitTexture != null) {
      var resolved = texture ?? PortraitPlaceholder;
      _portraitTexture.Texture = resolved;
      _portraitTexture.Visible = resolved != null;
    }

    if (_portraitLabel != null)
      _portraitLabel.Text = label ?? string.Empty;
  }

  private Texture2D PortraitPlaceholder =>
    _portraitPlaceholder ??= GD.Load<Texture2D>("res://Assets/Portraits/TODO.png");

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

  private void SetupTabUi() {
    if (_tabUi == null) return;
    _tabUi.TopLevel = true;
    _tabUi.Visible = false;
    UpdateTabUiSize(_tabUi);
    GetViewport().SizeChanged += () => UpdateTabUiSize(_tabUi);
  }

  private void UpdateTabUiSize(Control tabUi) {
    var viewport = GetViewport();
    if (viewport == null) return;
    tabUi.Size = viewport.GetVisibleRect().Size;
    tabUi.Position = Vector2.Zero;
  }

  // A transient, center-top banner built in code (no .tscn dependency) so event-driven UI
  // reactions have somewhere to land. Replace with a richer kill-feed / announcement widget later.
  private void SetupAnnouncement() {
    var defaultUi = GetNodeOrNull<Control>("DefaultUI");
    if (defaultUi == null) return;

    _announceLabel = new Label {
      Name = "Announcement",
      HorizontalAlignment = HorizontalAlignment.Center,
      VerticalAlignment = VerticalAlignment.Center,
      MouseFilter = Control.MouseFilterEnum.Ignore,
      AnchorLeft = 0f,
      AnchorRight = 1f,
      OffsetTop = 96f,
      OffsetBottom = 148f,
      GrowHorizontal = Control.GrowDirection.Both,
      Modulate = new Color(1f, 1f, 1f, 0f)
    };
    _announceLabel.AddThemeFontSizeOverride("font_size", 30);
    _announceLabel.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f));
    _announceLabel.AddThemeConstantOverride("outline_size", 3);
    defaultUi.AddChild(_announceLabel);
  }

  private void ShowAnnouncement(string text) {
    if (_announceLabel == null) return;
    _announceLabel.Text = text;
    _announceLabel.Modulate = new Color(1f, 1f, 1f);

    _announceTween?.Kill();
    _announceTween = CreateTween();
    _announceTween.TweenInterval(1.6);
    _announceTween.TweenProperty(_announceLabel, "modulate:a", 0f, 0.9);
  }

  // SubViewport inherits its parent viewport's World3D by default (own_world_3d = false), so
  // dropping an orthogonal top-down camera into it renders the same World scene live - no
  // duplicate geometry needed.
  private void SetupMinimap() {
    if (_minimapViewport == null) return;

    _minimapCamera = new Camera3D {
      Name = "MinimapCamera",
      Current = true,
      Projection = Camera3D.ProjectionType.Orthogonal,
      Size = MinimapOrthoSize,
      Near = 1.0f,
      Far = MinimapHeight + 50.0f,
      Position = new Vector3(0f, MinimapHeight, 0f),
      RotationDegrees = new Vector3(-90f, 0f, 0f)
    };
    _minimapViewport.AddChild(_minimapCamera);
  }

  private void SetTimerText(string text) {
    if (_timerLabel != null && _timerLabel.Text != text)
      _timerLabel.Text = text;
  }

  private static string FormatMatchTime(int seconds) {
    var m = seconds / 60;
    var s = seconds % 60;
    return $"{m}:{s:D2}";
  }
}
