using System;
using System.Collections.Generic;
using Godot;
using Meesles.Avalon.Client.Scripts.View;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Network;

namespace Meesles.Avalon;

public partial class LobbyUI : Control, IViewHud {
  private const int MaxSlots = 4;

  private readonly List<Button> _factionCards = [];
  private readonly Dictionary<int, Texture2D> _factionPortraits = new();

  // playerId -> factionId, fed by LobbyGameNode from the LobbyPlayerConfig broadcast. Slots with no
  // entry yet (config still in flight, or an empty slot) fall back to the placeholder portrait.
  private readonly Dictionary<int, int> _playerFactions = new();
  private readonly PlayerSlot[] _slots = new PlayerSlot[MaxSlots];

  private GridContainer _factionGrid;
  private LineEdit _ipField;
  private bool _isReady;
  private Button _joinButton;
  private bool _localReady;
  private Label _playerId;
  private LineEdit _nameField;
  private LineEdit _portField;
  private Button _readyButton;
  private Label _readyStatus;
  private Label _resultLabel;
  private PanelContainer _resultPanel;
  private Label _room;
  private Texture2D _placeholderPortrait;
  private Label _selectedFactionLabel;
  private TextureRect _selectedFactionPortrait;
  private Label _state;
  private Label _status;
  private Button _stopButton;
  private Label _timer;

  public string Host => _ipField?.Text?.Trim();
  public int Port => int.TryParse(_portField?.Text, out var p) ? p : 7777;

  public event Action OnJoinClicked;
  public event Action OnReadyClicked;
  public event Action OnUnreadyClicked;
  public event Action OnStopClicked;

  // Local pick changed. LobbyGameNode listens so it can re-broadcast the LobbyPlayerConfig.
  public event Action<int> OnFactionSelected;

  public override void _Ready() {
    const string left = "Root/Columns/LeftColumn/Margin/VBox";
    const string right = "Root/Columns/RightColumn/Margin/VBox";

    _nameField = GetNode<LineEdit>($"{left}/NameField");
    _selectedFactionPortrait = GetNode<TextureRect>($"{left}/SelectionRow/SelectedFactionPortrait");
    _selectedFactionLabel = GetNode<Label>($"{left}/SelectionRow/SelectedFactionLabel");
    _ipField = GetNode<LineEdit>($"{left}/HostRow/IpField");
    _portField = GetNode<LineEdit>($"{left}/PortRow/PortField");
    _room = GetNode<Label>($"{left}/InfoGrid/RoomLabel");
    _state = GetNode<Label>($"{left}/InfoGrid/StateLabel");
    _playerId = GetNode<Label>($"{left}/InfoGrid/PlayerIdLabel");
    _readyStatus = GetNode<Label>($"{left}/InfoGrid/ReadyStatusLabel");
    _timer = GetNode<Label>($"{left}/InfoGrid/TimerLabel");
    _status = GetNode<Label>($"{left}/StatusLabel");
    _joinButton = GetNode<Button>($"{left}/Buttons/JoinButton");
    _readyButton = GetNode<Button>($"{left}/Buttons/ReadyButton");
    _stopButton = GetNode<Button>($"{left}/Buttons/StopButton");

    _factionGrid = GetNode<GridContainer>("Root/Columns/MiddleColumn/FactionPanel/Margin/VBox/FactionGrid");

    for (var i = 0; i < MaxSlots; i++) {
      var row = $"{right}/PlayerSlots/Slot{i}/Margin/Row";
      _slots[i] = new PlayerSlot {
        Portrait = GetNode<TextureRect>($"{row}/Portrait"),
        Name = GetNode<Label>($"{row}/Info/NameLabel"),
        Status = GetNode<Label>($"{row}/Info/StatusLabel")
      };
    }

    _resultPanel = GetNode<PanelContainer>("ResultPanel");
    _resultLabel = GetNode<Label>("ResultPanel/ResultLabel");
    _resultPanel.Visible = false;

    _nameField.Text = PlayerProfile.PlayerName;
    // The roster clamps a claimed name to 62 UTF-8 bytes; 24 keeps it well inside that and inside the
    // width of a player row.
    _nameField.MaxLength = 24;
    _nameField.TextChanged += HandleNameChanged;
    _joinButton.Pressed += () => OnJoinClicked?.Invoke();
    _readyButton.Pressed += HandleReadyPressed;
    _stopButton.Pressed += () => OnStopClicked?.Invoke();

    BuildFactionCards();
    ClearSlots();
  }

  // -------------------------------------------------------------------- faction selection

  // One card per active faction, laid out in the middle column's grid. Cards are toggle buttons in
  // a shared ButtonGroup so exactly one stays lit; the pick mirrors into FactionSelection, which
  // SimCallbacks reads to send the SelectFactionCommand at match start.
  private void BuildFactionCards() {
    var defs = FactionCatalog.FactionDefs;
    _factionGrid.Columns = Math.Max(1, defs.Length);

    var group = new ButtonGroup();
    foreach (var def in defs) {
      var portrait = GD.Load<Texture2D>(def.PortraitTexturePath);
      if (portrait != null) _factionPortraits[def.Id] = portrait;

      var card = new Button {
        Name = $"FactionCard{def.Id}",
        ToggleMode = true,
        ButtonGroup = group,
        CustomMinimumSize = new Vector2(0, 180),
        SizeFlagsHorizontal = SizeFlags.ExpandFill,
        SizeFlagsVertical = SizeFlags.ExpandFill,
        TooltipText = def.Name
      };
      StyleFactionCard(card);

      // Button is not a container, so the contents are anchored to fill it manually.
      var margin = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore };
      margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
      foreach (var side in new[] { "left", "top", "right", "bottom" })
        margin.AddThemeConstantOverride($"margin_{side}", 10);
      card.AddChild(margin);

      var vbox = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
      vbox.AddThemeConstantOverride("separation", 6);
      margin.AddChild(vbox);

      var texture = new TextureRect {
        Texture = portrait ?? PlaceholderPortrait,
        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        SizeFlagsVertical = SizeFlags.ExpandFill,
        MouseFilter = MouseFilterEnum.Ignore
      };
      vbox.AddChild(texture);

      var label = new Label {
        Text = def.Name,
        HorizontalAlignment = HorizontalAlignment.Center,
        MouseFilter = MouseFilterEnum.Ignore
      };
      label.AddThemeFontSizeOverride("font_size", 16);
      vbox.AddChild(label);

      var factionId = def.Id;
      card.Pressed += () => SelectFaction(factionId);
      _factionGrid.AddChild(card);
      _factionCards.Add(card);
    }

    ApplySelectedFaction(FactionSelection.SelectedFactionId);
  }

  // The default button theme barely distinguishes pressed from normal, and the pick has to read at
  // a glance. Deliberately plain greys — this is placeholder styling, not a design.
  private static void StyleFactionCard(Button card) {
    card.AddThemeStyleboxOverride("normal",
      CardStyle(new Color(0.17f, 0.17f, 0.17f), new Color(0.3f, 0.3f, 0.3f), 1));
    card.AddThemeStyleboxOverride("hover",
      CardStyle(new Color(0.22f, 0.22f, 0.22f), new Color(0.45f, 0.45f, 0.45f), 1));
    card.AddThemeStyleboxOverride("pressed",
      CardStyle(new Color(0.26f, 0.26f, 0.26f), new Color(0.85f, 0.85f, 0.85f), 2));
    card.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
  }

  private static StyleBoxFlat CardStyle(Color background, Color border, int borderWidth) {
    var style = new StyleBoxFlat { BgColor = background, BorderColor = border };
    style.SetBorderWidthAll(borderWidth);
    return style;
  }

  private void SelectFaction(int factionId) {
    FactionSelection.SelectedFactionId = factionId;
    ApplySelectedFaction(factionId);
    OnFactionSelected?.Invoke(factionId);
  }

  // A peer's LobbyPlayerConfig arrived (or the local pick was mirrored back). The portrait lands on
  // the next SyncPlayers pass, which _Process drives every frame.
  public void SetPlayerFaction(int playerId, int factionId) {
    if (playerId > 0) _playerFactions[playerId] = factionId;
  }

  // Keeps the card toggles, the left-column summary and the local slot in sync with one pick.
  private void ApplySelectedFaction(int factionId) {
    var defs = FactionCatalog.FactionDefs;
    for (var i = 0; i < _factionCards.Count && i < defs.Length; i++)
      _factionCards[i].SetPressedNoSignal(defs[i].Id == factionId);

    _selectedFactionPortrait.Texture = ResolvePortrait(factionId);
    _selectedFactionLabel.Text = ResolveFactionName(factionId);
  }

  private Texture2D ResolvePortrait(int factionId) {
    return _factionPortraits.TryGetValue(factionId, out var tex) ? tex : PlaceholderPortrait;
  }

  private static string ResolveFactionName(int factionId) {
    foreach (var def in FactionCatalog.FactionDefs)
      if (def.Id == factionId)
        return def.Name;
    return "—";
  }

  private Texture2D PlaceholderPortrait =>
    _placeholderPortrait ??= GD.Load<Texture2D>("res://Assets/Portraits/TODO.png");

  private void HandleNameChanged(string text) {
    var trimmed = text?.Trim();
    PlayerProfile.PlayerName = string.IsNullOrEmpty(trimmed) ? PlayerProfile.DefaultName : trimmed;
  }

  // Used by the --name= launch arg, which has no field to type into.
  public void SetPlayerName(string name) {
    PlayerProfile.PlayerName = name;
    if (_nameField != null) _nameField.Text = name;
  }

  // ------------------------------------------------------------------- connection controls

  private void HandleReadyPressed() {
    if (_isReady) OnUnreadyClicked?.Invoke();
    else OnReadyClicked?.Invoke();
  }

  public void SetInitialHost(string host, int port) {
    if (_ipField != null) _ipField.Text = host;
    if (_portField != null) _portField.Text = port.ToString();
  }

  public void SetReadyState(bool ready) {
    _isReady = ready;
    if (_readyButton == null) return;
    _readyButton.Text = ready ? "Unready" : "Ready";
    _readyButton.Disabled = false;
  }

  public void SetReadyEnabled(bool enabled) {
    if (_readyButton != null) _readyButton.Disabled = !enabled;
  }

  public void SetStopEnabled(bool enabled) {
    if (_stopButton != null) _stopButton.Disabled = !enabled;
  }

  // ------------------------------------------------------------------------- lobby state

  public void SetLobbyMode() {
    _nameField.Editable = true;
    _state.Text = "—";
    _room.Text = "Not joined";
    _playerId.Text = "—";
    _readyStatus.Text = "No";
    _status.Text = "Not connected";
    _timer.Text = "—";
    ClearSlots();
    HideResult();
  }

  public void SetConnected(bool connected, int roomId = 0) {
    _room.Text = connected ? $"#{roomId}" : "Not joined";
    // The name is claimed in the join handshake, so edits after joining would never reach the roster.
    // Lock the field rather than letting it drift out of sync with what other players see.
    _nameField.Editable = !connected;
    if (connected) return;

    _localReady = false;
    _readyStatus.Text = "No";
    _status.Text = "Not connected";
    // Player ids are reassigned on the next join, so stale faction rows would mislabel new slots.
    _playerFactions.Clear();
    ClearSlots();
  }

  public void SetLocalReady(bool ready) {
    _localReady = ready;
    _readyStatus.Text = ready ? "Yes" : "No";
  }

  public void SetPhase(SessionPhase phase) {
    _state.Text = phase.ToString();
    _status.Text = phase switch {
      SessionPhase.None => "Not connected",
      SessionPhase.Synchronized => "Waiting for players to ready up",
      SessionPhase.Countdown => "Match starting",
      SessionPhase.Playing => "In game",
      SessionPhase.Disconnected => "Disconnected",
      _ => phase.ToString()
    };
  }

  public void SetCountdownRemaining(double seconds) {
    if (seconds < 0) seconds = 0;
    _timer.Text = $"{seconds:0.0}s";
  }

  public void SetLocalPlayerId(int? playerId) {
    if (playerId.HasValue && playerId.Value >= 0) {
      var displayId = playerId.Value <= 0 ? 1 : playerId.Value;
      _playerId.Text = $"P{displayId}";
    }
    else {
      _playerId.Text = "—";
    }
  }

  // Right column: one row per match slot. Connected players fill from the top, the local player
  // carries their own name and faction portrait, and unused slots stay greyed out.
  public void SyncPlayers(IReadOnlyList<IPlayerInfo> players, int localPlayerId) {
    SetLocalPlayerId(localPlayerId > 0 ? localPlayerId : null);
    // The local pick renders immediately rather than waiting on the server's echo of our own config.
    if (localPlayerId > 0) _playerFactions[localPlayerId] = FactionSelection.SelectedFactionId;

    var i = 0;
    var localShown = false;

    foreach (var p in players) {
      if (i >= MaxSlots) break;
      if (p.PlayerId == localPlayerId) localShown = true;
      FillSlot(i++, p.PlayerId, p.DisplayName, p.IsReady, p.PlayerId == localPlayerId);
    }

    // Local player not yet in the network roster — synthesize from known local state.
    if (!localShown && localPlayerId > 0 && i < MaxSlots)
      FillSlot(i++, localPlayerId, null, _localReady, true);

    for (; i < MaxSlots; i++)
      ClearSlot(i);
  }

  private void FillSlot(int index, int playerId, string displayName, bool ready, bool isLocal) {
    var slot = _slots[index];
    var name = isLocal
      ? PlayerProfile.PlayerName
      : string.IsNullOrWhiteSpace(displayName)
        ? $"P{playerId}"
        : displayName;

    slot.Name.Text = isLocal ? $"{name} (you)" : name;
    slot.Name.Modulate = Colors.White;
    slot.Status.Text = ready ? "Ready" : "Waiting...";
    slot.Portrait.Texture = _playerFactions.TryGetValue(playerId, out var factionId)
      ? ResolvePortrait(factionId)
      : PlaceholderPortrait;
    slot.Portrait.Modulate = Colors.White;
  }

  private void ClearSlot(int index) {
    var slot = _slots[index];
    slot.Name.Text = "Empty slot";
    slot.Name.Modulate = new Color(1, 1, 1, 0.45f);
    slot.Status.Text = "—";
    slot.Portrait.Texture = PlaceholderPortrait;
    slot.Portrait.Modulate = new Color(1, 1, 1, 0.25f);
  }

  private void ClearSlots() {
    for (var i = 0; i < MaxSlots; i++)
      ClearSlot(i);
  }

  // ----------------------------------------------------------------------------- IViewHud

  public void SyncFromFrame(Frame frame) {
    // LobbyUI shows lobby state, not ECS frame data
  }

  public void ShowResult(string text) {
    _resultPanel.Visible = true;
    _resultLabel.Text = text;
  }

  public void HideResult() {
    _resultPanel.Visible = false;
  }

  private class PlayerSlot {
    public Label Name;
    public TextureRect Portrait;
    public Label Status;
  }
}
