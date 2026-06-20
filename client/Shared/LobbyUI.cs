using global::Godot;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Network;

namespace Meesles.Avalon {
  public partial class LobbyUI : Control, IViewHud {
    private Label _state;
    private Label _room;
    private Label _playerId;
    private Label _readyStatus;
    private Label _playerRow0;
    private Label _playerRow1;
    private Label _status;
    private Label _timer;
    private Panel _resultPanel;
    private Label _resultLabel;
    private bool _localReady;

    public override void _Ready() {
      _state = GetNode<Label>("StateLabel");
      _room = GetNode<Label>("RoomLabel");
      _playerId = GetNode<Label>("PlayerIdLabel");
      _readyStatus = GetNode<Label>("ReadyStatusLabel");
      _playerRow0 = GetNode<Label>("PlayerRow0Label");
      _playerRow1 = GetNode<Label>("PlayerRow1Label");
      _status = GetNode<Label>("StatusLabel");
      _timer = GetNode<Label>("TimerLabel");
      _resultPanel = GetNode<Panel>("ResultPanel");
      _resultLabel = GetNode<Label>("ResultPanel/ResultLabel");
      _resultPanel.Visible = false;
    }

    public void SetLobbyMode() {
      _state.Text = "—";
      _room.Text = "Not joined";
      _playerId.Text = "—";
      _readyStatus.Text = "No";
      _status.Text = "Not connected";
      _timer.Text = "—";
      _playerRow0.Text = "—";
      _playerRow1.Text = "—";
      HideResult();
    }

    public void SetMultiplayerMode() {
      _state.Text = "Starting";
      _room.Text = "—";
      _playerId.Text = "—";
      _readyStatus.Text = "No";
      _status.Text = "Connecting...";
      _timer.Text = "0s";
      _playerRow0.Text = "—";
      _playerRow1.Text = "—";
      HideResult();
    }

    public void SetSandboxMode() {
      _state.Text = "Local Sandbox";
      _room.Text = "Offline";
      _playerId.Text = "—";
      _readyStatus.Text = "—";
      _status.Text = "Move: WASD or arrow keys";
      _timer.Text = "Idle";
      HideResult();
    }

    public void SetConnected(bool connected, int roomId = 0) {
      _room.Text = connected ? $"#{roomId}" : "Not joined";
      if (!connected) {
        _localReady = false;
        _readyStatus.Text = "No";
        _status.Text = "Not connected";
        _playerRow0.Text = "—";
        _playerRow1.Text = "—";
      }
    }

    public void SetLocalPlayerId(int? playerId) {
      if (playerId.HasValue && playerId.Value >= 0) {
        int displayId = playerId.Value <= 0 ? 1 : playerId.Value;
        _playerId.Text = $"P{displayId}";
      }
      else {
        _playerId.Text = "—";
      }
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
        _ => phase.ToString(),
      };
    }

    public void SetCountdownRemaining(double seconds) {
      if (seconds < 0) seconds = 0;
      _timer.Text = $"{seconds:0.0}s";
    }

    public void SetStartDelayRemaining(double seconds) {
      if (seconds < 0) seconds = 0;
      _timer.Text = $"Start: {seconds:0.0}s";
    }

    public void SyncPlayers(System.Collections.Generic.IReadOnlyList<IPlayerInfo> players, int localPlayerId) {
      SetLocalPlayerId(localPlayerId > 0 ? localPlayerId : (int?)null);

      var rows = new[] { _playerRow0, _playerRow1 };
      int i = 0;
      bool localShown = false;

      foreach (var p in players) {
        if (p.PlayerId == localPlayerId) localShown = true;
        if (i < rows.Length) {
          string suffix = p.PlayerId == localPlayerId ? " (you)" : "";
          rows[i].Text = $"P{p.PlayerId}: {(p.IsReady ? "Ready" : "Waiting...")}{suffix}";
          i++;
        }
      }

      // Local player not yet in the network roster — synthesize from known local state.
      if (!localShown && localPlayerId > 0 && i < rows.Length) {
        rows[i].Text = $"P{localPlayerId}: {(_localReady ? "Ready" : "Waiting...")} (you)";
        i++;
      }

      for (; i < rows.Length; i++)
        rows[i].Text = "—";
      // _readyStatus is owned by SetLocalReady; don't overwrite it here.
    }

    public void SyncFromFrame(Frame frame) {
      // LobbyUI shows lobby state, not ECS frame data
    }

    public void SyncSandbox(Vector3 position, Vector3 movement) {
      _status.Text = $"Position: {position.X:0.0}, {position.Z:0.0}";
      _timer.Text = movement.LengthSquared() > 0.001f ? "Moving" : "Idle";
    }

    public void ShowResult(string text) {
      _resultPanel.Visible = true;
      _resultLabel.Text = text;
    }

    public void HideResult() {
      _resultPanel.Visible = false;
    }

  }
}
