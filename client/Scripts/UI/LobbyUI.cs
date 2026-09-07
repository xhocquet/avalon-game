using System;
using System.Collections.Generic;
using Godot;
using Meesles.Avalon.Client.Scripts.View;
using Meesles.Avalon.Sim;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Network;

namespace Meesles.Avalon;

public partial class LobbyUI : Control, IViewHud {
  private const int MaxSlots = 4;
  private const float FactionPortraitSize = 112;

  private readonly List<Button> _factionCards = [];
  private readonly List<Button> _gameTypeCards = [];
  private readonly Dictionary<int, Texture2D> _factionPortraits = new();

  // playerId -> factionId, fed by LobbyGameNode from the LobbyPlayerConfig broadcast. Slots with no
  // entry yet (config still in flight, or an empty slot) fall back to the placeholder portrait.
  private readonly Dictionary<int, int> _playerFactions = new();
  private readonly PlayerSlot[] _slots = new PlayerSlot[MaxSlots];

  private GridContainer _factionGrid;
  private GridContainer _gameTypeGrid;
  private Button _startButton;
  private Button _disconnectButton;
  private LineEdit _ipField;
  private bool _isConnected;
  private bool _isReady;
  private Button _joinButton;
  private bool _localReady;
  private int? _localPlayerId;
  private LineEdit _nameField;
  private LineEdit _portField;
  private Button _readyButton;
  private Label _resultLabel;
  private PanelContainer _resultPanel;
  private Texture2D _placeholderPortrait;
  private Label _status;
  private SessionPhase _phase = SessionPhase.None;
  private double? _countdownSeconds;
  private int? _roomId;

  public string Host => _ipField?.Text?.Trim();
  public int Port => int.TryParse(_portField?.Text, out var p) ? p : ServerEndpoint.Port;

  public event Action OnJoinClicked;
  public event Action OnDisconnectClicked;
  public event Action OnReadyClicked;
  public event Action OnUnreadyClicked;

  // Local pick changed. LobbyGameNode listens so it can re-broadcast the LobbyPlayerConfig.
  public event Action<int> OnFactionSelected;

  // The "Start" button a local game type shows instead of Join/Ready.
  public event Action OnStartLocalClicked;

  public override void _Ready() {
    const string left = "Root/Columns/LeftColumn/Margin/VBox";

    _nameField = GetNode<LineEdit>($"{left}/NameField");
    _ipField = GetNode<LineEdit>($"{left}/HostRow/IpField");
    _portField = GetNode<LineEdit>($"{left}/HostRow/PortField");
    _status = GetNode<Label>($"{left}/StatusLabel");
    _startButton = GetNode<Button>($"{left}/Buttons/StartButton");
    _joinButton = GetNode<Button>($"{left}/JoinRow/JoinButton");
    _disconnectButton = GetNode<Button>($"{left}/JoinRow/DisconnectButton");
    _readyButton = GetNode<Button>($"{left}/Buttons/ReadyButton");

    _factionGrid = GetNode<GridContainer>("Root/Columns/MiddleColumn/FactionPanel/Margin/VBox/FactionGrid");
    _gameTypeGrid = GetNode<GridContainer>($"{left}/GameTypeGrid");

    for (var i = 0; i < MaxSlots; i++) {
      var row = $"{left}/PlayerScroll/PlayerSlots/Slot{i}/Margin/Row";
      _slots[i] = new PlayerSlot {
        Portrait = GetNode<TextureRect>($"{row}/PortraitFrame/Portrait"),
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
    _disconnectButton.Pressed += () => OnDisconnectClicked?.Invoke();
    _readyButton.Pressed += HandleReadyPressed;
    _startButton.Pressed += () => OnStartLocalClicked?.Invoke();

    BuildFactionCards();
    BuildGameTypeCards();
    ClearSlots();
  }

  // -------------------------------------------------------------------- game type selection

  // One compact toggle button per GameTypeCatalog entry.
  private void BuildGameTypeCards() {
    var defs = GameTypeCatalog.GameTypes;
    _gameTypeGrid.Columns = Math.Max(1, defs.Length);

    var group = new ButtonGroup();
    foreach (var def in defs) {
      var card = new Button {
        Name = $"GameTypeCard{def.Id}",
        Text = def.Name,
        ToggleMode = true,
        ButtonGroup = group,
        CustomMinimumSize = new Vector2(0, 36),
        SizeFlagsHorizontal = SizeFlags.ExpandFill
      };
      StyleFactionCard(card);

      var id = def.Id;
      card.Pressed += () => SelectGameType(id);
      _gameTypeGrid.AddChild(card);
      _gameTypeCards.Add(card);
    }

    ApplySelectedGameType(GameTypeSelection.SelectedGameTypeId);
  }

  // Used by the --gametype= launch arg, which has no card to click.
  public void SetGameType(string id) {
    SelectGameType(id);
  }

  private void SelectGameType(string id) {
    GameTypeSelection.SelectedGameTypeId = id;
    ApplySelectedGameType(id);
  }

  // A local game type has no server to join and no roster to ready up against, so the connection
  // controls swap for a single Start.
  private void ApplySelectedGameType(string id) {
    var defs = GameTypeCatalog.GameTypes;
    for (var i = 0; i < _gameTypeCards.Count && i < defs.Length; i++)
      _gameTypeCards[i].SetPressedNoSignal(defs[i].Id == id);

    var selected = GameTypeCatalog.Resolve(id);
    var local = selected.IsLocal;
    _startButton.Visible = local;
    _joinButton.Visible = !local;
    _disconnectButton.Visible = !local;
    _readyButton.Visible = !local;
    if (local) _status.Text = "Local session — press Start";
    else if (!_isConnected) _status.Text = "Not connected";
  }

  // Locked while a session is live: the pick decides which map the sim loaded, so changing it mid
  // session would only desync the lobby's own labels.
  public void SetGameTypeEnabled(bool enabled) {
    foreach (var card in _gameTypeCards)
      card.Disabled = !enabled;
  }

  // -------------------------------------------------------------------- faction selection

  // One card per active faction, laid out in the middle column's grid. Cards are toggle buttons in
  // a shared ButtonGroup so exactly one stays lit; the pick mirrors into FactionSelection, which
  // SimCallbacks reads to send the SelectFactionCommand at match start.
  private void BuildFactionCards() {
    while (_factionGrid.GetChildCount() > 0) {
      var child = _factionGrid.GetChild(0);
      _factionGrid.RemoveChild(child);
      child.QueueFree();
    }

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
        CustomMinimumSize = new Vector2(0, 160),
        SizeFlagsHorizontal = SizeFlags.ExpandFill,
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

      var portraitFrame = new Control {
        CustomMinimumSize = new Vector2(FactionPortraitSize, FactionPortraitSize),
        SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
        SizeFlagsVertical = SizeFlags.ShrinkCenter,
        ClipContents = true,
        MouseFilter = MouseFilterEnum.Ignore
      };
      vbox.AddChild(portraitFrame);

      var texture = new TextureRect {
        Texture = portrait ?? PlaceholderPortrait,
        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
        MouseFilter = MouseFilterEnum.Ignore
      };
      texture.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
      portraitFrame.AddChild(texture);

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

  // Keeps the card toggles in sync with one pick.
  private void ApplySelectedFaction(int factionId) {
    var defs = FactionCatalog.FactionDefs;
    for (var i = 0; i < _factionCards.Count && i < defs.Length; i++)
      _factionCards[i].SetPressedNoSignal(defs[i].Id == factionId);

  }

  private Texture2D ResolvePortrait(int factionId) {
    return _factionPortraits.TryGetValue(factionId, out var tex) ? tex : PlaceholderPortrait;
  }

  private Texture2D PlaceholderPortrait =>
    _placeholderPortrait ??= GD.Load<Texture2D>("res://Assets/Placeholders/brown-swirl.webp");

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

  private void SetReadyState(bool ready) {
    _isReady = ready;
    if (_readyButton == null) return;
    _readyButton.Text = ready ? "Unready" : "Ready";
  }

  public void SetReadyEnabled(bool enabled) {
    if (_readyButton != null) _readyButton.Disabled = !enabled;
  }

  // ------------------------------------------------------------------------- lobby state

  public void SetLobbyMode() {
    _nameField.Editable = true;
    _phase = SessionPhase.None;
    _roomId = null;
    _countdownSeconds = null;
    SetLocalReady(false);
    UpdateStatus();
    _joinButton.Disabled = false;
    _disconnectButton.Disabled = true;
    ClearSlots();
    HideResult();
    ApplySelectedGameType(GameTypeSelection.SelectedGameTypeId);
  }

  public void SetConnected(bool connected, int roomId = 0) {
    _isConnected = connected;
    _joinButton.Disabled = connected;
    _disconnectButton.Disabled = !connected;
    _roomId = connected ? roomId : null;
    if (!connected) _countdownSeconds = null;
    // The name is claimed in the join handshake, so edits after joining would never reach the roster.
    // Lock the field rather than letting it drift out of sync with what other players see.
    _nameField.Editable = !connected;
    UpdateStatus();
    if (connected) return;

    SetLocalReady(false);
    // Player ids are reassigned on the next join, so stale faction rows would mislabel new slots.
    _playerFactions.Clear();
    ClearSlots();
  }

  public void SetLocalReady(bool ready) {
    _localReady = ready;
    SetReadyState(ready);
  }

  public void SetPhase(SessionPhase phase) {
    _phase = phase;
    if (phase != SessionPhase.Countdown) _countdownSeconds = null;
    UpdateStatus();
  }

  public void SetCountdownRemaining(double seconds) {
    if (seconds < 0) seconds = 0;
    _countdownSeconds = seconds;
    UpdateStatus();
  }

  private void UpdateStatus() {
    var text = _phase switch {
      SessionPhase.None => "Not connected",
      SessionPhase.Synchronized => "Waiting for players to ready up",
      SessionPhase.Countdown => "Match starting",
      SessionPhase.Playing => "In game",
      SessionPhase.Disconnected => "Disconnected",
      _ => _phase.ToString()
    };
    if (_roomId is int roomId) text = $"#{roomId} · {text}";
    if (_phase == SessionPhase.Countdown && _countdownSeconds is double seconds)
      text = $"{text} · {seconds:0.0}s";
    _status.Text = text;
  }

  public void SetLocalPlayerId(int? playerId) {
    _localPlayerId = playerId is int id && id >= 0 ? id : null;
  }

  // Player list: one row per match slot. Connected players fill from the top, the local player
  // carries their own name and faction portrait, and unused slots stay greyed out.
  public void SyncPlayers(IReadOnlyList<IPlayerInfo> players, int localPlayerId) {
    SetLocalPlayerId(localPlayerId > 0 ? localPlayerId : null);
    // The local pick renders immediately rather than waiting on the server's echo of our own config.
    if (localPlayerId > 0) _playerFactions[localPlayerId] = FactionSelection.SelectedFactionId;

    var i = 0;
    var localShown = false;

    foreach (var p in players) {
      if (i >= MaxSlots) break;
      if (p.PlayerId == localPlayerId) {
        localShown = true;
        SetLocalReady(p.IsReady);
      }
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

  // The lobby has no room for the scoreboard the in-game panel shows, so it takes the one-liner.
  public void ShowResult(MatchResult result) {
    _resultPanel.Visible = true;
    _resultLabel.Text = MatchResultText.Summary(result, _localPlayerId);
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
