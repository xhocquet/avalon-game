using System;
using System.Collections.Generic;
using Godot;
using Meesles.Avalon.Client.Scripts.Entities;
using Meesles.Avalon.Client.Scripts.Interfaces;
using Meesles.Avalon.Client.Scripts.View;
using Meesles.Avalon.Sim;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Network;

namespace Meesles.Avalon;

public partial class GameUI : CanvasLayer, IViewHud {
  private const uint WorldAndMinimapLayers = (1u << 0) | (1u << 1);
  private readonly List<IDisposable> _eventSubscriptions = new();
  private Label _announceLabel;
  private Tween _announceTween;
  private Label _pingLabel;
  private ColorRect _damageGlow;
  private Tween _damageTween;
  private int _localUnitId;

  public void SetPing(int milliseconds) {
    _pingLabel.Visible = milliseconds >= 0;
    _pingLabel.Text = $"{milliseconds} ms";
  }

  private void OnLocalDamage(AttackHitEvent evt) {
    if (evt.TargetUnitId != _localUnitId || _localUnitId == 0 || evt.Damage.ToFloat() <= 0) return;
    _damageTween?.Kill();
    _damageTween = CreateTween();
    _damageTween.TweenProperty(_damageGlow, "modulate:a", 1f, 0.12);
    _damageTween.TweenInterval(0.6);
    _damageTween.TweenProperty(_damageGlow, "modulate:a", 0f, 1.8);
  }

  private SimEventHub _events;
  private Label _focusTargetLabel;
  private ColorRect _healthBar;
  private ColorRect _healthBarFill;
  private Label _healthBarLabel;
  private ColorRect _manaBar;
  private ColorRect _manaBarFill;
  private Label _manaBarLabel;
  private ColorRect _xpBar;
  private ColorRect _xpBarFill;
  private Label _goldLabel;
  private Label _resourcesLabel;
  private Label _levelLabel;
  private Label _attackDamageLabel;
  private Label _attackSpeedLabel;
  private Label _armorLabel;
  private Label _magicResistLabel;
  private Label _critLabel;
  private Label _moveSpeedLabel;
  private Label _attackRangeLabel;
  private Label _healthRegenLabel;
  private TextureButton _statsButton;
  private PanelContainer _statsPopup;
  private int? _localPlayerId;
  private Label _resultLabel;
  private Label _resultReasonLabel;
  private GridContainer _resultScoreboard;
  private Button _resultReturnButton;
  private Panel _resultPanel;
  private Label _scoreboardScoreLabel;
  private Control _selectionRectangle;
  private Control _tabUi;

  private ActionBarController _actionBar;
  private Meesles.Avalon.Client.ActionGridPopup _actionPopup;
  public bool IsActionPopupOpen => _actionPopup?.Visible == true;
  private SkillBarController _skillBar;
  private BuffBarController _buffBar;
  private InventoryPanelController _inventoryPanel;
  private ShopItemCatalog _shopCatalog;
  private SkillCatalog _skillCatalog;
  private ShopEntity _contextShop;

  // Set by InputCapture.BindGameUI: raised when the player clicks a shop buy button. InputCapture
  // turns the invocation into a PurchaseItemCommand.
  public Action<int> PurchaseRequested { get; set; }

  // Same wiring for the skill tree: the argument is a SkillSlot index. SkillBarController raises the
  // upgrade one when a skill cell is clicked; nothing raises the cast one yet.
  public Action<int> SkillUpgradeRequested { get; set; }
  public Action<int> SkillCastRequested { get; set; }

  // Raised by the end-of-match panel's button. GameNode tears the session down and swaps the scene.
  public Action ReturnToLobbyRequested { get; set; }

  // Upgrades queued but not yet run by the sim. InputCapture writes it as it queues commands and gates
  // against it; the skill bar paints through it. Shared rather than owned by either so the button the
  // player clicked and the rule that approves the next click read the same optimistic state.
  public PredictedSkillState PredictedSkills { get; } = new();

  // Same arrangement for shop buys: InputCapture writes it as it queues PurchaseItemCommands and gates
  // against it, the gold counter, the buy grid and the item panel all paint through it.
  public PredictedPurchaseState PredictedPurchases { get; } = new();

  private Label _timerLabel;
  private SubViewport _minimapViewport;
  private Camera3D _minimapCamera;
  private VisionRenderer _visionRenderer;
  private TextureRect _portraitTexture;
  private Texture2D _portraitPlaceholder;
  [Export] public float FocusRingRadiusPx { get; set; } = 52.0f;
  [Export] public float FocusRingWidthPx { get; set; } = 2.5f;
  [Export] public Color FocusRingColor { get; set; } = new(0.88f, 0.72f, 0.22f, 0.92f);

  // Covers the World.tscn ground plane (100x100, centered at origin).
  [Export] public float MinimapOrthoSize { get; set; } = 100.0f;
  [Export] public float MinimapHeight { get; set; } = 60.0f;

  public void SyncFromFrame(Frame frame) {
    if (_visionRenderer == null) {
      _visionRenderer = new VisionRenderer { Name = "VisionRenderer" };
      AddChild(_visionRenderer);
    }

    _visionRenderer.Sync(frame, _localPlayerId);
    if (_scoreboardScoreLabel != null)
      _scoreboardScoreLabel.Text = FormatLiveScores(frame);

    UpdateLocalPlayerHealth(frame);
    UpdateLocalPlayerInventory(frame);
    UpdateLocalPlayerStats(frame);
    UpdateLocalPlayerExperience(frame);
    _skillBar?.Update(frame, _localPlayerId);
    _buffBar?.Update(frame, _localPlayerId);
    _actionBar?.Update(frame, _localPlayerId, _contextShop);
    _inventoryPanel?.Update(frame, _localPlayerId);

    var elapsed = frame.Tick * (double)frame.DeltaTimeMs / 1000.0;
    SetTimerText(FormatMatchTime((int)elapsed));
  }

  // "P1 4 / P2 7", in player-id order rather than entity order so the reading doesn't shuffle tick to
  // tick. Sized to the players actually on the board, not a fixed two.
  private static string FormatLiveScores(Frame frame) {
    var scores = new SortedDictionary<int, int>();
    var playerFilter = frame.Filter<Hero, Player>();
    while (playerFilter.Next(out var entity))
      scores[frame.GetReadOnly<Hero>(entity).PlayerId] = frame.GetReadOnly<Player>(entity).Score;

    var parts = new List<string>(scores.Count);
    foreach (var (playerId, score) in scores)
      parts.Add($"P{playerId} {score}");

    return parts.Count > 0 ? string.Join(" / ", parts) : "0 / 0";
  }

  public void SetLocalPlayerId(int? playerId) {
    _localUnitId = 0;
    _damageTween?.Kill();
    if (_damageGlow != null) _damageGlow.Modulate = new Color(1, 1, 1, 0);
    _localPlayerId = playerId is int id && id >= 0 ? id : null;
    // Session boundary - in-flight upgrades and buys from the previous one are void.
    PredictedSkills.Clear();
    PredictedPurchases.Clear();
    _buffBar?.Clear();
    SetContextShop(null);
  }

  public void ShowResult(MatchResult result) {
    if (_resultLabel != null) _resultLabel.Text = MatchResultText.Headline(result, _localPlayerId);
    if (_resultReasonLabel != null) _resultReasonLabel.Text = MatchResultText.Reason(result);
    BuildScoreboard(result);
    SetContextShop(null);
    if (_resultPanel != null) _resultPanel.Visible = true;
    if (_resultReturnButton != null) {
      _resultReturnButton.Disabled = false;
      _resultReturnButton.GrabFocus();
    }
  }

  // Disabled on the way out: the scene change is deferred a frame, and a second press in that window
  // would tear the session down twice.
  private void OnReturnToLobbyPressed() {
    if (_resultReturnButton != null) _resultReturnButton.Disabled = true;
    ReturnToLobbyRequested?.Invoke();
  }

  public void HideResult() {
    if (_resultPanel != null) _resultPanel.Visible = false;
  }

  private static readonly string[] ScoreboardColumns =
    ["Player", "Faction", "Score", "K / D", "Minions", "Structures", "Damage", "Level"];

  private void BuildScoreboard(MatchResult result) {
    if (_resultScoreboard == null) return;

    foreach (var child in _resultScoreboard.GetChildren())
      child.QueueFree();

    foreach (var column in ScoreboardColumns)
      _resultScoreboard.AddChild(ScoreboardCell(column, header: true));

    if (result.Players == null) return;

    foreach (var player in result.Players) {
      // The winning side is the only thing distinguishing rows - player names ride the join
      // handshake, which never reaches the sim, so the row is identified by player id.
      var won = player.IsWinner;
      _resultScoreboard.AddChild(ScoreboardCell($"P{player.PlayerId}", won: won));
      _resultScoreboard.AddChild(ScoreboardCell(FactionName(player.FactionId), won: won));
      _resultScoreboard.AddChild(ScoreboardCell($"{player.Score}", won: won));
      _resultScoreboard.AddChild(ScoreboardCell($"{player.HeroKills} / {player.Deaths}", won: won));
      _resultScoreboard.AddChild(ScoreboardCell($"{player.MinionKills}", won: won));
      _resultScoreboard.AddChild(ScoreboardCell($"{player.StructureKills}", won: won));
      _resultScoreboard.AddChild(ScoreboardCell($"{player.DamageDealt}", won: won));
      _resultScoreboard.AddChild(ScoreboardCell($"{player.Level}", won: won));
    }
  }

  private static Label ScoreboardCell(string text, bool header = false, bool won = false) {
    var label = new Label { Text = text, HorizontalAlignment = HorizontalAlignment.Center };
    label.AddThemeFontSizeOverride("font_size", header ? 13 : 16);
    label.AddThemeColorOverride("font_color", header
      ? new Color(0.62f, 0.63f, 0.66f)
      : won
        ? new Color(0.95f, 0.82f, 0.42f)
        : new Color(0.86f, 0.87f, 0.9f));
    return label;
  }

  // Name only - loading a FactionCatalog here would pull in every hero scene for a text column.
  private static string FactionName(int factionId) {
    foreach (var def in FactionCatalog.FactionDefs)
      if (def.Id == factionId)
        return def.Name;

    return "-";
  }

  public override void _Ready() {
    var overlay = new Control { Name = "FeedbackOverlay", MouseFilter = Control.MouseFilterEnum.Ignore };
    AddChild(overlay);
    overlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
    _damageGlow = new ColorRect {
      Name = "DamageGlow",
      MouseFilter = Control.MouseFilterEnum.Ignore,
      Material = new ShaderMaterial { Shader = GD.Load<Shader>("res://Assets/FX/Shaders/DamageVignette.gdshader") },
      Modulate = new Color(1, 1, 1, 0)
    };
    overlay.AddChild(_damageGlow);
    _damageGlow.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
    _pingLabel = new Label {
      Name = "Ping",
      Visible = false,
      MouseFilter = Control.MouseFilterEnum.Ignore,
      HorizontalAlignment = HorizontalAlignment.Right
    };
    overlay.AddChild(_pingLabel);
    _pingLabel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopRight);
    _pingLabel.OffsetLeft = -150;
    _pingLabel.OffsetRight = -16;
    _pingLabel.OffsetTop = 54;
    _pingLabel.AddThemeFontSizeOverride("font_size", 18);
    _pingLabel.AddThemeConstantOverride("outline_size", 4);
    _pingLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
    ProcessMode = ProcessModeEnum.Always;
    SetProcessInput(true);

    _timerLabel = GetNode<Label>("DefaultUI/Timer");
    _focusTargetLabel = GetNodeOrNull<Label>("DefaultUI/Focus");
    _tabUi = GetNode<Control>("TabUI");
    _scoreboardScoreLabel = GetNode<Label>("TabUI/ScoreboardPanel/ResultPanelFrame/Header/ScoreLabel");
    const string bottomBar = "DefaultUI/BottomBar/Panels";
    _healthBar = GetNode<ColorRect>("%HealthBar");
    _healthBarFill = GetNode<ColorRect>("%HealthBarFill");
    _healthBarLabel = GetNode<Label>("%HealthBarLabel");
    _manaBar = GetNode<ColorRect>("%ManaBar");
    _manaBarFill = GetNode<ColorRect>("%ManaBarFill");
    _manaBarLabel = GetNode<Label>("%ManaBarLabel");
    _statsButton = GetNode<TextureButton>("%StatsButton");
    _statsPopup = GetNode<PanelContainer>("%StatsPopup");
    _statsPopup.AddThemeStyleboxOverride("panel", _statsButton.GetThemeStylebox("panel", "TooltipPanel"));
    _statsButton.Pressed += () => {
      _statsPopup.Visible = !_statsPopup.Visible;
      if (_statsPopup.Visible) PositionStatsPopup();
    };
    var statsRoot = _statsPopup.GetNode<Control>("StatsList");
    _levelLabel = GetNode<Label>("%LevelLabel");
    _goldLabel = GetNode<Label>("%GoldLabel");
    _resourcesLabel = GetNode<Label>("%ResourceLabel");
    _attackDamageLabel = statsRoot?.GetNodeOrNull<Label>("CombatStatList/AttackDamageValue");
    _attackSpeedLabel = statsRoot?.GetNodeOrNull<Label>("CombatStatList/AttackSpeedValue");
    _armorLabel = statsRoot?.GetNodeOrNull<Label>("CombatStatList/ArmorValue");
    _magicResistLabel = statsRoot?.GetNodeOrNull<Label>("CombatStatList/MagicResistanceValue");
    _critLabel = statsRoot?.GetNodeOrNull<Label>("CombatStatList/CriticalChanceValue");
    _moveSpeedLabel = statsRoot?.GetNodeOrNull<Label>("CombatStatList/MoveSpeedValue");
    _attackRangeLabel = statsRoot?.GetNodeOrNull<Label>("CombatStatList/AttackRangeValue");
    _healthRegenLabel = statsRoot?.GetNodeOrNull<Label>("CombatStatList/HealthRegenerationValue");
    _xpBar = GetNode<ColorRect>("%XpBar");
    _xpBarFill = GetNode<ColorRect>("%XpBarFill");
    _selectionRectangle = GetNode<Control>("DefaultUI/SelectionRectangle");
    _resultPanel = GetNodeOrNull<Panel>("DefaultUI/ResultPanel");
    _resultLabel = GetNodeOrNull<Label>("DefaultUI/ResultPanel/Content/ResultLabel");
    _resultReasonLabel = GetNodeOrNull<Label>("DefaultUI/ResultPanel/Content/ReasonLabel");
    _resultScoreboard = GetNodeOrNull<GridContainer>("DefaultUI/ResultPanel/Content/Scoreboard");
    _resultReturnButton = GetNodeOrNull<Button>("DefaultUI/ResultPanel/Content/Footer/ReturnButton");
    if (_resultReturnButton != null)
      _resultReturnButton.Pressed += OnReturnToLobbyPressed;
    _minimapViewport = GetNode<SubViewport>("%MinimapViewport");
    _portraitTexture = GetNodeOrNull<TextureRect>(
      "%PortraitTexture");

    var actionGrid = GetNodeOrNull<GridContainer>(
      "%SkillGrid");
    _shopCatalog = ShopItemCatalog.CreateDefault();
    _skillCatalog = SkillCatalog.CreateDefault();
    _buffBar = new BuffBarController(GetNodeOrNull<HBoxContainer>(
      $"{bottomBar}/Vbox/BuffMarginContainer/BuffContainer"), _skillCatalog);

    _skillBar = new SkillBarController(actionGrid, _skillCatalog, slot => SkillUpgradeRequested?.Invoke(slot),
      PredictedSkills);
    _actionPopup = GetNode<Meesles.Avalon.Client.ActionGridPopup>("%ActionGridPopup");
    _actionBar = new ActionBarController(_actionPopup, _shopCatalog, itemId => PurchaseRequested?.Invoke(itemId),
      PredictedPurchases);

    var itemPanel = GetNodeOrNull<GridContainer>(
      "%ItemPanel");
    _inventoryPanel = new InventoryPanelController(itemPanel, _shopCatalog, PredictedPurchases);

    SetSelectionRectangle(null);
    if (_resultPanel != null) _resultPanel.Visible = false;
    SetupTabUi();
    SetupAnnouncement();
    SetupMinimap();
    SetFocusPortrait(null);
  }

  public override void _ExitTree() {
    UnbindSimEvents();
    _announceTween?.Kill();
    _damageTween?.Kill();
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
    _eventSubscriptions.Add(hub.OnConfirmed<HeroLeveledUpEvent>(OnHeroLeveledUp));
    _eventSubscriptions.Add(hub.OnConfirmed<AttackHitEvent>(OnLocalDamage));
  }

  private void UnbindSimEvents() {
    foreach (var sub in _eventSubscriptions)
      sub.Dispose();
    _eventSubscriptions.Clear();
    _events = null;
  }

  private void OnCrystalDestroyed(CrystalDestroyedEvent evt) {
    ShowAnnouncement("Container Destroyed!");
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

  private void OnHeroLeveledUp(HeroLeveledUpEvent evt) {
    if (_localPlayerId is int id && evt.PlayerId == id)
      ShowAnnouncement($"Level Up!  Level {evt.Level}");
  }

  public override void _Input(InputEvent @event) {
    if (IsActionPopupOpen) return;
    if (_statsPopup.Visible) {
      if (@event is InputEventKey { Pressed: true, Keycode: Key.Escape }) {
        _statsPopup.Hide();
        GetViewport().SetInputAsHandled();
        return;
      }
      if (@event is InputEventMouseButton popupMouse &&
          _statsPopup.GetGlobalRect().HasPoint(popupMouse.Position)) {
        GetViewport().SetInputAsHandled();
        return;
      }
      if (@event is InputEventMouseButton { Pressed: true } mouse &&
          !_statsPopup.GetGlobalRect().HasPoint(mouse.Position) &&
          !_statsButton.GetGlobalRect().HasPoint(mouse.Position))
        _statsPopup.Hide();
    }

    if (UiFocus.IsTypingInTextField(GetViewport()))
      return;

    if (@event is InputEventKey key && key.Keycode == Key.Tab && !key.Echo) {
      if (_tabUi != null) _tabUi.Visible = key.Pressed;
      GetViewport().SetInputAsHandled();
    }
  }

  public override void _Process(double delta) {
    if (_statsPopup.Visible) PositionStatsPopup();
  }

  private void PositionStatsPopup() {
    _statsPopup.Size = _statsPopup.GetCombinedMinimumSize();
    var bounds = GetViewport().GetVisibleRect();
    var position = _statsButton.GlobalPosition - new Vector2(0, _statsPopup.Size.Y + 6);
    position.X = Mathf.Clamp(position.X, bounds.Position.X, Mathf.Max(bounds.Position.X, bounds.End.X - _statsPopup.Size.X));
    position.Y = Mathf.Clamp(position.Y, bounds.Position.Y, Mathf.Max(bounds.Position.Y, bounds.End.Y - _statsPopup.Size.Y));
    _statsPopup.GlobalPosition = position.Round();
  }

  private void UpdateLocalPlayerHealth(Frame frame) {
    if (_localPlayerId is not int localId) return;

    var filter = frame.Filter<Hero, Health, Stats>();
    while (filter.Next(out var entity)) {
      ref readonly var hero = ref frame.GetReadOnly<Hero>(entity);
      if (hero.PlayerId != localId) continue;

      ref readonly var health = ref frame.GetReadOnly<Health>(entity);
      _localUnitId = UnitLookup.GetUnitId(ref frame, entity);
      ref readonly var stats = ref frame.GetReadOnly<Stats>(entity);
      SetPlayerHealth(health.Current.ToFloat(), stats.MaxHealth.ToFloat());
      SetPlayerMana(health.Mana.ToFloat(), stats.MaxMana.ToFloat());
      return;
    }
  }

  // Runs before the shop and item controllers paint, so the optimistic buys they read have already
  // been aged or retired against this frame.
  private void UpdateLocalPlayerInventory(Frame frame) {
    if (_localPlayerId is not int localId) return;

    var filter = frame.Filter<Hero, Inventory>();
    while (filter.Next(out var entity)) {
      ref readonly var hero = ref frame.GetReadOnly<Hero>(entity);
      if (hero.PlayerId != localId) continue;

      ref readonly var inventory = ref frame.GetReadOnly<Inventory>(entity);
      foreach (var def in ShopItemCatalog.ItemDefs)
        PredictedPurchases.Observe(def.Id, inventory.CountOf(def.Id));

      SetGoldText(inventory.Gold - PredictedPurchases.PendingGold);
      SetResourcesText(frame.Has<Resources>(entity)
        ? frame.GetReadOnly<Resources>(entity).Total
        : 0);
      return;
    }
  }

  private void SetGoldText(int gold) {
    if (_goldLabel != null)
      _goldLabel.Text = $"{(gold < 0 ? 0 : gold)} G"; // a rollback can shrink gold under what's in flight
  }

  private void SetResourcesText(int resources) {
    if (_resourcesLabel != null)
      _resourcesLabel.Text = $"{resources} Res.";
  }

  private void UpdateLocalPlayerStats(Frame frame) {
    if (_localPlayerId is not int localId) return;

    var filter = frame.Filter<Hero, Stats>();
    while (filter.Next(out var entity)) {
      ref readonly var hero = ref frame.GetReadOnly<Hero>(entity);
      if (hero.PlayerId != localId) continue;

      SetHeroStats(frame.GetReadOnly<Stats>(entity));
      return;
    }
  }

  // Reads effective stats, including item and buff modifiers.
  private void SetHeroStats(in Stats stats) {
    SetStatValue(_attackDamageLabel, $"{stats.AttackDamage.ToFloat():0.#}");
    SetStatValue(_attackSpeedLabel, $"{stats.AttacksPerSecond.ToFloat():0.00}");
    SetStatValue(_armorLabel, $"{stats.Armor.ToFloat():0.#}");
    SetStatValue(_magicResistLabel, $"{stats.MagicResist.ToFloat():0.#}");
    SetStatValue(_critLabel, $"{stats.CritChance.ToFloat() * 100f:0.#}%");
    SetStatValue(_moveSpeedLabel, $"{stats.MoveSpeed.ToFloat():0.#}");
    SetStatValue(_attackRangeLabel, $"{stats.AttackRange.ToFloat():0.#}");
    SetStatValue(_healthRegenLabel, $"{stats.HealthRegen.ToFloat():0.#}/5s"); // authored per 5 seconds
  }

  private static void SetStatValue(Label label, string value) {
    if (label != null)
      label.Text = value;
  }

  private void UpdateLocalPlayerExperience(Frame frame) {
    if (_localPlayerId is not int localId) return;
    if (!frame.AssetRegistry.TryGet<XpRulesAsset>(out var rules)) return;

    var filter = frame.Filter<Hero, Experience>();
    while (filter.Next(out var entity)) {
      ref readonly var hero = ref frame.GetReadOnly<Hero>(entity);
      if (hero.PlayerId != localId) continue;

      ref readonly var experience = ref frame.GetReadOnly<Experience>(entity);
      SetPlayerExperience(experience.Level, experience.Xp, rules);
      return;
    }
  }

  // Experience.Xp is lifetime XP against cumulative thresholds, so the bar shows
  // progress through the current level only: the span between this level's threshold and the next.
  public void SetPlayerExperience(int level, int experience, XpRulesAsset rules) {
    if (_levelLabel != null)
      _levelLabel.Text = $"{level}";

    if (_xpBar == null || _xpBarFill == null || rules == null) return;

    var atMaxLevel = level >= rules.MaxLevel;
    var levelStart = rules.TotalXpForLevel(level);
    var nextLevel = rules.TotalXpForLevel(level + 1);
    var needed = nextLevel - levelStart;
    var into = experience - levelStart;

    var ratio = atMaxLevel || needed <= 0 ? 1f : Mathf.Clamp(into / (float)needed, 0f, 1f);
    var fillHeight = _xpBar.Size.Y * ratio;
    _xpBarFill.Position = new Vector2(0, _xpBar.Size.Y - fillHeight);
    _xpBarFill.Size = new Vector2(_xpBar.Size.X, fillHeight);
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

  private void SetPlayerMana(float current, float maximum) {
    if (_manaBar == null || _manaBarFill == null) return;
    var ratio = maximum <= 0f ? 0f : Mathf.Clamp(current / maximum, 0f, 1f);
    _manaBarFill.Size = new Vector2(_manaBar.Size.X * ratio, _manaBar.Size.Y);
    if (_manaBarLabel != null)
      _manaBarLabel.Text = $"MP {(int)current} / {(int)maximum}";
  }

  // Driven by InputCapture's selection system - shows the portrait of the currently selected
  // InputCapture resolves the selected hero or minion faction portrait before this paints it.
  public void SetFocusPortrait(Texture2D texture) {
    if (_portraitTexture != null) {
      var resolved = texture ?? PortraitPlaceholder;
      _portraitTexture.Texture = resolved;
      _portraitTexture.Visible = resolved != null;
    }
  }

  private Texture2D PortraitPlaceholder =>
    _portraitPlaceholder ??= GD.Load<Texture2D>("res://Assets/Portraits/TODO.png");

  // A new selection also reopens a dismissed shop popup.
  public void SetContextShop(ShopEntity shop) {
    _contextShop = shop;
    _actionBar?.ResetContext();
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

  private void SetupTabUi() {
    if (_tabUi == null) return;
    _tabUi.TopLevel = true;
    _tabUi.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
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
      RotationDegrees = new Vector3(-90f, 0f, 0f),
      CullMask = WorldAndMinimapLayers
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
