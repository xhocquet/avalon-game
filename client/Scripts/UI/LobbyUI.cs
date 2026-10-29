using System;
using System.Collections.Generic;
using Godot;
using Meesles.Avalon.Client.Scripts.GameState;
using Meesles.Avalon.Client.Scripts.Interfaces;
using Meesles.Avalon.Sim;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Network;

namespace Meesles.Avalon;

public partial class LobbyUI : Control, IViewHud {
  private const int SlotsPerSide = 3;

  private readonly List<Button> _factionCards = [];
  private readonly List<(NinePatchRect Frame, Texture2D DefaultTexture)> _factionFrames = [];
  private readonly Dictionary<int, Texture2D> _factionPortraits = new();

  // playerId -> factionId, fed by LobbyGameNode from the LobbyPlayerConfig broadcast. Slots with no
  // entry yet (config still in flight, or an empty slot) fall back to the placeholder portrait.
  private readonly Dictionary<int, int> _playerFactions = new();
  private readonly PlayerSlot[] _slots = new PlayerSlot[SlotsPerSide * 2];

  private GridContainer _factionGrid;
  private ChampionPreview _championPreview;
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
  private VBoxContainer _buttonContent;
  private Label _buttonTitle;
  private Label _resultLabel;
  private PanelContainer _resultPanel;
  private Texture2D _placeholderPortrait;
  private Texture2D _selectedFactionBorder;
  private Label _status;
  private SessionPhase _phase = SessionPhase.None;
  private double? _countdownSeconds;
  private int? _roomId;

  public string Host => _ipField?.Text?.Trim();
  public int Port => int.TryParse(_portField?.Text, out var p) ? p : ServerConfig.Port;

  public event Action OnJoinClicked;
  public event Action OnDisconnectClicked;
  public event Action OnReadyClicked;
  public event Action OnUnreadyClicked;

  // Local pick changed. LobbyGameNode listens so it can re-broadcast the LobbyPlayerConfig.
  public event Action<int> OnFactionSelected;

  // The "Start" button a local game type shows instead of Join/Ready.
  public event Action OnStartLocalClicked;

  public override void _Ready() {
    const string bottom = "Root/BottomBar/Margin/Row";

    _nameField = GetNode<LineEdit>("%NameField");
    _ipField = GetNode<LineEdit>("%IpField");
    _portField = GetNode<LineEdit>("%PortField");
    _buttonContent = GetNode<VBoxContainer>($"{bottom}/MiddleColumn/Center/Buttons/ReadyButton/Content");
    _buttonTitle = _buttonContent.GetNode<Label>("Title");
    _status = _buttonContent.GetNode<Label>("StatusLabel");
    _startButton = GetNode<Button>($"{bottom}/MiddleColumn/Center/Buttons/StartButton");
    _joinButton = GetNode<Button>("%JoinButton");
    _disconnectButton = GetNode<Button>("%DisconnectButton");
    _readyButton = GetNode<Button>($"{bottom}/MiddleColumn/Center/Buttons/ReadyButton");

    _factionGrid = GetNode<GridContainer>("%FactionGrid");
    _championPreview = GetNode<ChampionPreview>("%ChampionPreview");

    for (var i = 0; i < _slots.Length; i++) {
      var side = i < SlotsPerSide ? "LeftColumn/Allies" : "RightColumn/Enemies";
      var row = $"{bottom}/{side}/Slots/Slot{i % SlotsPerSide}/Margin/Row";
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

    BindPages();
    BindFactionCards();
    ApplySelectedGameType(GameTypeSelection.SelectedGameTypeId);
    ClearSlots();
  }

  private void BindPages() {
    const string tabs = "Root/TopBar/Margin/Row/MiddleColumn/Center/Tabs";
    var lobbyPage = GetNode<Control>("Root/Columns");
    var bottomBar = GetNode<Control>("Root/BottomBar");
    var lobbyFooter = GetNode<Control>("Root/BottomBar/Margin");
    var vaultPage = GetNode<Control>("%VaultPage");
    var settingsPage = GetNode<Control>("%SettingsPage");
    var settingsFooter = GetNode<Control>("Root/BottomBar/SettingsButtons");
    var settingsPanel = settingsPage.GetNode<Control>("SettingsPanel");
    var heroPanel = GetNode<Control>("Root/Columns/MiddleColumn");

    void UpdateSettingsWidth() {
      var halfWidth = Mathf.Min(heroPanel.Size.X, settingsPage.Size.X / 2);
      settingsPanel.OffsetLeft = -halfWidth;
      settingsPanel.OffsetRight = halfWidth;
    }

    heroPanel.Resized += UpdateSettingsWidth;
    settingsPage.Resized += UpdateSettingsWidth;
    UpdateSettingsWidth();

    GetNode<Button>($"{tabs}/Play").Toggled += selected => {
      lobbyPage.Visible = selected;
      lobbyFooter.Visible = selected;
      bottomBar.Visible = selected;
    };
    GetNode<Button>($"{tabs}/Vault").Toggled += selected => {
      vaultPage.Visible = selected;
      bottomBar.Visible = !selected;
    };
    GetNode<Button>($"{tabs}/Settings").Toggled += selected => {
      settingsPage.Visible = selected;
      settingsFooter.Visible = selected;
      bottomBar.Visible = selected;
    };
  }

  // -------------------------------------------------------------------- game type selection

  // Used by the --gametype= launch arg.
  public void SetGameType(string id) {
    GameTypeSelection.SelectedGameTypeId = id;
    ApplySelectedGameType(id);
  }

  // A local game type has no server to join and no roster to ready up against, so the connection
  // controls swap for a single Start.
  private void ApplySelectedGameType(string id) {
    var selected = GameTypeCatalog.Resolve(id);
    var local = selected.IsLocal;
    _startButton.Visible = local;
    _joinButton.Visible = !local;
    _disconnectButton.Visible = !local;
    _readyButton.Visible = !local;
    var activeButton = local ? _startButton : _readyButton;
    if (_buttonContent.GetParent() != activeButton) _buttonContent.Reparent(activeButton, false);
    _buttonTitle.Text = local ? "Start" : _isReady ? "Unready" : "Ready";
    if (local) _status.Text = "Local session — press Start";
    else if (!_isConnected) _status.Text = "Not connected";
  }

  // -------------------------------------------------------------------- faction selection

  // One card per active faction, laid out in the middle column's grid. Cards are toggle buttons in
  // a shared ButtonGroup so exactly one stays lit; the pick mirrors into FactionSelection, which
  // SimCallbacks reads to send the SelectFactionCommand at match start.
  private void BindFactionCards() {
    _selectedFactionBorder = GD.Load<Texture2D>("res://Assets/UI/PanelBorderGreyDetail.png");
    var defs = FactionCatalog.FactionDefs;
    foreach (var def in defs) {
      var portrait = GD.Load<Texture2D>(def.HeroPortraitTexturePath);
      if (portrait != null) _factionPortraits[def.Id] = portrait;

      var card = _factionGrid.GetNode<Button>($"FactionCard{def.Id}");
      var texture = card.GetNode<TextureRect>("%PortraitTexture");
      texture.Texture = portrait ?? PlaceholderPortrait;
      var frame = card.GetNode<NinePatchRect>("%PortraitFrame");
      _factionFrames.Add((frame, frame.Texture));
      var label = card.GetNode<Label>("%FactionName");
      label.Text = def.Name;
      var factionId = def.Id;
      card.Pressed += () => SelectFaction(factionId);
      _factionCards.Add(card);
    }

    ApplySelectedFaction(FactionSelection.SelectedFactionId);
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
    _championPreview.ShowFaction(factionId);
    var defs = FactionCatalog.FactionDefs;
    for (var i = 0; i < _factionCards.Count && i < defs.Length; i++) {
      var selected = defs[i].Id == factionId;
      _factionCards[i].SetPressedNoSignal(selected);
      var (frame, defaultTexture) = _factionFrames[i];
      frame.Texture = selected ? _selectedFactionBorder : defaultTexture;
    }
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
    if (_buttonContent.GetParent() == _readyButton) _buttonTitle.Text = ready ? "Unready" : "Ready";
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

  // Each player has their own team in the current match setup.
  public void SyncPlayers(IReadOnlyList<IPlayerInfo> players, int localPlayerId) {
    SetLocalPlayerId(localPlayerId > 0 ? localPlayerId : null);
    // The local pick renders immediately rather than waiting on the server's echo of our own config.
    if (localPlayerId > 0) _playerFactions[localPlayerId] = FactionSelection.SelectedFactionId;

    ClearSlots();
    var enemyIndex = SlotsPerSide;
    var localShown = false;

    foreach (var p in players) {
      if (p.PlayerId == localPlayerId) {
        localShown = true;
        SetLocalReady(p.IsReady);
        FillSlot(0, p.PlayerId, p.DisplayName, p.IsReady, true);
      }
      else if (enemyIndex < _slots.Length) {
        FillSlot(enemyIndex++, p.PlayerId, p.DisplayName, p.IsReady, false);
      }
    }

    // Local player not yet in the network roster — synthesize from known local state.
    if (!localShown && localPlayerId > 0)
      FillSlot(0, localPlayerId, null, _localReady, true);
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
    slot.Name.Text = "Empty";
    slot.Name.Modulate = new Color(1, 1, 1, 0.45f);
    slot.Status.Text = "—";
    slot.Portrait.Texture = PlaceholderPortrait;
    slot.Portrait.Modulate = new Color(1, 1, 1, 0.25f);
  }

  private void ClearSlots() {
    for (var i = 0; i < _slots.Length; i++)
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
