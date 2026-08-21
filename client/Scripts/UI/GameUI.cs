using System;
using System.Collections.Generic;
using Godot;
using Meesles.Avalon.Client.Scripts.View;
using Meesles.Avalon.Sim;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
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
  private ColorRect _xpBar;
  private ColorRect _xpBarFill;
  private Label _xpBarLabel;
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
  private SkillBarController _skillBar;
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
    if (_scoreboardScoreLabel != null)
      _scoreboardScoreLabel.Text = FormatLiveScores(frame);

    UpdateLocalPlayerHealth(frame);
    UpdateLocalPlayerInventory(frame);
    UpdateLocalPlayerStats(frame);
    UpdateLocalPlayerExperience(frame);
    _skillBar?.Update(frame, _localPlayerId);
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
    _localPlayerId = playerId is int id && id >= 0 ? id : null;
    // Session boundary - in-flight upgrades and buys from the previous one are void.
    PredictedSkills.Clear();
    PredictedPurchases.Clear();
  }

  public void ShowResult(MatchResult result) {
    if (_resultLabel != null) _resultLabel.Text = MatchResultText.Headline(result, _localPlayerId);
    if (_resultReasonLabel != null) _resultReasonLabel.Text = MatchResultText.Reason(result);
    BuildScoreboard(result);
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
    ProcessMode = ProcessModeEnum.Always;
    SetProcessInput(true);

    _timerLabel = GetNode<Label>("DefaultUI/Timer");
    _focusTargetLabel = GetNodeOrNull<Label>("DefaultUI/Focus");
    _tabUi = GetNode<Control>("TabUI");
    _scoreboardScoreLabel = GetNode<Label>("TabUI/ScoreboardPanel/Header/ScoreLabel");
    _healthBar = GetNode<ColorRect>("DefaultUI/BottomBar/MarginContainer/Panels/Vbox/HealthBar");
    _healthBarFill = GetNode<ColorRect>("DefaultUI/BottomBar/MarginContainer/Panels/Vbox/HealthBar/HealthBarFill");
    _healthBarLabel = GetNode<Label>("DefaultUI/BottomBar/MarginContainer/Panels/Vbox/HealthBar/HealthBarLabel");
    var statsRoot = GetNodeOrNull<Control>(
      "DefaultUI/BottomBar/MarginContainer/Panels/Vbox/MainSection/MinionAndStatsPanel/Margin/Stats");
    _levelLabel = statsRoot?.GetNodeOrNull<Label>("LevelLabel");
    _goldLabel = statsRoot?.GetNodeOrNull<Label>("EconomyRow/GoldLabel");
    _resourcesLabel = statsRoot?.GetNodeOrNull<Label>("EconomyRow/ResourcesLabel");
    _attackDamageLabel = statsRoot?.GetNodeOrNull<Label>("StatGrid/AttackDamageLabel");
    _attackSpeedLabel = statsRoot?.GetNodeOrNull<Label>("StatGrid/AttackSpeedLabel");
    _armorLabel = statsRoot?.GetNodeOrNull<Label>("StatGrid/ArmorLabel");
    _magicResistLabel = statsRoot?.GetNodeOrNull<Label>("StatGrid/MagicResistLabel");
    _critLabel = statsRoot?.GetNodeOrNull<Label>("StatGrid/CritLabel");
    _moveSpeedLabel = statsRoot?.GetNodeOrNull<Label>("StatGrid/MoveSpeedLabel");
    _attackRangeLabel = statsRoot?.GetNodeOrNull<Label>("StatGrid/AttackRangeLabel");
    _healthRegenLabel = statsRoot?.GetNodeOrNull<Label>("StatGrid/HealthRegenLabel");
    _xpBar = GetNodeOrNull<ColorRect>("DefaultUI/BottomBar/MarginContainer/Panels/Vbox/XpBar");
    _xpBarFill = GetNodeOrNull<ColorRect>("DefaultUI/BottomBar/MarginContainer/Panels/Vbox/XpBar/XpBarFill");
    _xpBarLabel = GetNodeOrNull<Label>("DefaultUI/BottomBar/MarginContainer/Panels/Vbox/XpBar/XpBarLabel");
    _selectionRectangle = GetNode<Control>("DefaultUI/SelectionRectangle");
    _resultPanel = GetNodeOrNull<Panel>("DefaultUI/ResultPanel");
    _resultLabel = GetNodeOrNull<Label>("DefaultUI/ResultPanel/Content/ResultLabel");
    _resultReasonLabel = GetNodeOrNull<Label>("DefaultUI/ResultPanel/Content/ReasonLabel");
    _resultScoreboard = GetNodeOrNull<GridContainer>("DefaultUI/ResultPanel/Content/Scoreboard");
    _resultReturnButton = GetNodeOrNull<Button>("DefaultUI/ResultPanel/Content/Footer/ReturnButton");
    if (_resultReturnButton != null)
      _resultReturnButton.Pressed += OnReturnToLobbyPressed;
    _minimapViewport =
      GetNodeOrNull<SubViewport>("DefaultUI/BottomBar/MarginContainer/Panels/MinimapContainer/MinimapViewport");
    _portraitTexture = GetNodeOrNull<TextureRect>(
      "DefaultUI/BottomBar/MarginContainer/Panels/Vbox/MainSection/HeroMarginPanel/VBox/PortraitTexture");
    _portraitLabel = GetNodeOrNull<Label>(
      "DefaultUI/BottomBar/MarginContainer/Panels/Vbox/MainSection/HeroMarginPanel/VBox/PortraitLabel");

    var actionGrid = GetNodeOrNull<GridContainer>(
      "DefaultUI/BottomBar/MarginContainer/Panels/ActionMContainer/ActionGrid");
    _shopCatalog = ShopItemCatalog.CreateDefault();
    _skillCatalog = SkillCatalog.CreateDefault();

    // Order matters: the skill bar claims the grid's leading cells, then the action bar is told how many
    // to leave alone. Building them the other way round would let the action bar clear the skill cells.
    _skillBar = new SkillBarController(actionGrid, _skillCatalog, slot => SkillUpgradeRequested?.Invoke(slot),
      PredictedSkills);
    _actionBar = new ActionBarController(actionGrid, _shopCatalog, itemId => PurchaseRequested?.Invoke(itemId),
      SkillBarController.SlotCount, PredictedPurchases);

    var itemPanel = GetNodeOrNull<GridContainer>(
      "DefaultUI/BottomBar/MarginContainer/Panels/Vbox/MainSection/MarginContainer/ItemPanel");
    _inventoryPanel = new InventoryPanelController(itemPanel, _shopCatalog, PredictedPurchases);

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
    _eventSubscriptions.Add(hub.OnConfirmed<HeroLeveledUpEvent>(OnHeroLeveledUp));
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

  private void OnHeroLeveledUp(HeroLeveledUpEvent evt) {
    if (_localPlayerId is int id && evt.PlayerId == id)
      ShowAnnouncement($"Level Up!  Level {evt.Level}");
  }

  public override void _Input(InputEvent @event) {
    if (UiFocus.IsTypingInTextField(GetViewport()))
      return;

    if (@event is InputEventKey key && key.Keycode == Key.Tab && !key.Echo) {
      if (_tabUi != null) _tabUi.Visible = key.Pressed;
      GetViewport().SetInputAsHandled();
    }
  }

  private void UpdateLocalPlayerHealth(Frame frame) {
    if (_localPlayerId is not int localId) return;

    var filter = frame.Filter<Hero, Health, StatsComponent>();
    while (filter.Next(out var entity)) {
      ref readonly var hero = ref frame.GetReadOnly<Hero>(entity);
      if (hero.PlayerId != localId) continue;

      ref readonly var health = ref frame.GetReadOnly<Health>(entity);
      SetPlayerHealth(health.Current.ToFloat(),
        frame.GetReadOnly<StatsComponent>(entity).MaxHealth.ToFloat());
      return;
    }
  }

  // Runs before the shop and item controllers paint, so the optimistic buys they read have already
  // been aged or retired against this frame.
  private void UpdateLocalPlayerInventory(Frame frame) {
    if (_localPlayerId is not int localId) return;

    var filter = frame.Filter<Hero, InventoryComponent>();
    while (filter.Next(out var entity)) {
      ref readonly var hero = ref frame.GetReadOnly<Hero>(entity);
      if (hero.PlayerId != localId) continue;

      ref readonly var inventory = ref frame.GetReadOnly<InventoryComponent>(entity);
      foreach (var def in ShopItemCatalog.ItemDefs)
        PredictedPurchases.Observe(def.Id, inventory.CountOf(def.Id));

      SetGoldText(inventory.Gold - PredictedPurchases.PendingGold);
      SetResourcesText(frame.Has<ResourcesComponent>(entity)
        ? frame.GetReadOnly<ResourcesComponent>(entity).Total
        : 0);
      return;
    }
  }

  private void SetGoldText(int gold) {
    if (_goldLabel != null)
      _goldLabel.Text = $"Gold {(gold < 0 ? 0 : gold)}"; // a rollback can shrink gold under what's in flight
  }

  private void SetResourcesText(int resources) {
    if (_resourcesLabel != null)
      _resourcesLabel.Text = $"Resources {resources}";
  }

  private void UpdateLocalPlayerStats(Frame frame) {
    if (_localPlayerId is not int localId) return;

    var filter = frame.Filter<Hero, StatsComponent>();
    while (filter.Next(out var entity)) {
      ref readonly var hero = ref frame.GetReadOnly<Hero>(entity);
      if (hero.PlayerId != localId) continue;

      SetHeroStats(frame.GetReadOnly<StatsComponent>(entity));
      return;
    }
  }

  // Reads the live StatsComponent, which already carries item and buff contributions - timed buffs
  // Add into it and record what they moved so the expiry can take the same amount back off.
  private void SetHeroStats(in StatsComponent stats) {
    SetStatText(_attackDamageLabel, "Attack dmg", $"{stats.AttackDamage.ToFloat():0.#}");
    SetStatText(_attackSpeedLabel, "Attack spd", $"{stats.AttacksPerSecond.ToFloat():0.00}");
    SetStatText(_armorLabel, "Armor", $"{stats.Armor.ToFloat():0.#}");
    SetStatText(_magicResistLabel, "Magic res", $"{stats.MagicResist.ToFloat():0.#}");
    SetStatText(_critLabel, "Crit", $"{stats.CritChance.ToFloat() * 100f:0.#}%");
    SetStatText(_moveSpeedLabel, "Move spd", $"{stats.MoveSpeed.ToFloat():0.#}");
    SetStatText(_attackRangeLabel, "Range", $"{stats.AttackRange.ToFloat():0.#}");
    SetStatText(_healthRegenLabel, "HP regen", $"{stats.HealthRegen.ToFloat():0.#}/5s"); // authored per 5 seconds
  }

  private static void SetStatText(Label label, string name, string value) {
    if (label != null)
      label.Text = $"{name} {value}";
  }

  private void UpdateLocalPlayerExperience(Frame frame) {
    if (_localPlayerId is not int localId) return;
    if (!frame.AssetRegistry.TryGet<XpRulesAsset>(out var rules)) return;

    var filter = frame.Filter<Hero, ExperienceComponent>();
    while (filter.Next(out var entity)) {
      ref readonly var hero = ref frame.GetReadOnly<Hero>(entity);
      if (hero.PlayerId != localId) continue;

      ref readonly var experience = ref frame.GetReadOnly<ExperienceComponent>(entity);
      SetPlayerExperience(experience.Level, experience.Experience, rules);
      return;
    }
  }

  // ExperienceComponent.Experience is lifetime XP against cumulative thresholds, so the bar shows
  // progress through the current level only: the span between this level's threshold and the next.
  public void SetPlayerExperience(int level, int experience, XpRulesAsset rules) {
    if (_levelLabel != null)
      _levelLabel.Text = $"Level {level}";

    if (_xpBar == null || _xpBarFill == null || rules == null) return;

    var atMaxLevel = level >= rules.MaxLevel;
    var levelStart = rules.TotalXpForLevel(level);
    var nextLevel = rules.TotalXpForLevel(level + 1);
    var needed = nextLevel - levelStart;
    var into = experience - levelStart;

    var ratio = atMaxLevel || needed <= 0 ? 1f : Mathf.Clamp(into / (float)needed, 0f, 1f);
    _xpBarFill.Size = new Vector2(_xpBar.Size.X * ratio, _xpBar.Size.Y);

    if (_xpBarLabel != null)
      _xpBarLabel.Text = atMaxLevel
        ? $"MAX ({experience} XP)"
        : $"{(int)(ratio * 100f)}% ({into} / {needed})";
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

  // InputCapture reports which shop (if any) is currently selected for inspection. The action bar
  // re-evaluates proximity every frame, so we just store the reference here.
  public void SetContextShop(ShopEntity shop) {
    _contextShop = shop;
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
