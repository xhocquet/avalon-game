using global::Godot;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Network;

namespace Meesles.Avalon {
  public partial class Hud : Control {
    private bool _sandboxMode = true;
    private Label _state;
    private Label _playerId;
    private Label _score0;
    private Label _score1;
    private Label _timer;
    private Label _minionCount;
    private Panel _resultPanel;
    private Label _resultLabel;

    public override void _Ready() {
      _state = GetNode<Label>("StateLabel");
      _playerId = GetNode<Label>("PlayerIdLabel");
      _score0 = GetNode<Label>("Score0Label");
      _score1 = GetNode<Label>("Score1Label");
      _timer = GetNode<Label>("TimerLabel");
      _minionCount = GetNode<Label>("MinionCountLabel");
      _resultPanel = GetNode<Panel>("ResultPanel");
      _resultLabel = GetNode<Label>("ResultPanel/ResultLabel");
      _resultPanel.Visible = false;
      if (_sandboxMode) SetSandboxMode();
    }

    public void SetSandboxMode() {
      _sandboxMode = true;
      _state.Text = "Local Sandbox";
      SetLocalPlayerId(null);
      _score0.Text = "Move: WASD or arrow keys";
      _score1.Text = "Reset: button on the left";
      _timer.Text = "Idle";
    }

    public void SetMultiplayerMode() {
      _sandboxMode = false;
      _state.Text = "Starting";
      SetLocalPlayerId(null);
      _score0.Text = "P1: 0";
      _score1.Text = "P2: 0";
      _timer.Text = "0s";
      _minionCount.Text = "Minions: 0";
      HideStatus();
    }

    public void SetLobbyMode() {
      _sandboxMode = false;
      _state.Text = "Lobby";
      SetLocalPlayerId(null);
      _score0.Text = "Waiting for players";
      _score1.Text = "Ready up";
      _timer.Text = "0s";
      _minionCount.Text = "";
      HideStatus();
    }

    public void SetLocalPlayerId(int? playerId) {
      if (playerId.HasValue && playerId.Value >= 0) {
        int displayId = playerId.Value <= 0 ? 1 : playerId.Value;
        _playerId.Text = $"You: P{displayId}";
        _playerId.Visible = true;
      }
      else {
        _playerId.Text = "";
        _playerId.Visible = false;
      }
    }

    public void SyncSandbox(Vector3 position, Vector3 movement) {
      if (!_sandboxMode) return;
      _score1.Text = $"Position: {position.X:0.0}, {position.Z:0.0}";
      _timer.Text = movement.LengthSquared() > 0.001f ? "Moving" : "Idle";
    }

    public void ShowStatus(string text) {
      _resultPanel.Visible = true;
      _resultLabel.Text = text;
    }

    public void HideStatus() {
      _resultPanel.Visible = false;
    }

    public void SetPhase(SessionPhase _) {
      if (_sandboxMode) return;
      _state.Text = _.ToString();
    }

    public void SetCountdownRemaining(double seconds) {
      if (_sandboxMode) return;
      if (seconds < 0) seconds = 0;
      _timer.Text = $"{seconds:0.0}s";
    }

    public void SetStartDelayRemaining(double seconds) {
      if (_sandboxMode) return;
      if (seconds < 0) seconds = 0;
      _timer.Text = $"Start: {seconds:0.0}s";
    }

    public void SetLocalReady(bool ready) {
      if (_sandboxMode) _timer.Text = ready ? "Ready" : "Idle";
      else _state.Text = ready ? "Ready" : _state.Text;
    }

    public void SyncFromFrame(Frame frame) {
      if (_sandboxMode) return;

      int p1 = 0;
      int p2 = 0;
      var filter = frame.Filter<PlayerComponent>();
      while (filter.Next(out var entity)) {
        ref readonly var player = ref frame.GetReadOnly<PlayerComponent>(entity);
        if (player.PlayerId <= 1) p1 = player.Score;
        else if (player.PlayerId == 2) p2 = player.Score;
      }

      _score0.Text = $"P1: {p1}";
      _score1.Text = $"P2: {p2}";

      int m1 = 0;
      int m2 = 0;
      int mOther = 0;
      var minionFilter = frame.Filter<Minion, Team>();
      while (minionFilter.Next(out var entity)) {
        ref readonly var team = ref frame.GetReadOnly<Team>(entity);
        if (team.TeamId == 1) m1++;
        else if (team.TeamId == 2) m2++;
        else mOther++;
      }
      int total = m1 + m2 + mOther;
      _minionCount.Text = $"Minions: {total}  (T1: {m1}  T2: {m2})";

      double remaining = frame.AssetRegistry.Get<PlayerStatsAsset>().MatchDuration.ToDouble()
          - ((frame.Tick * frame.DeltaTimeMs) / 1000.0);
      if (remaining < 0) remaining = 0;
      _timer.Text = $"{remaining:0.0}s";
    }

    public void ShowResult(string text) {
      ShowStatus(text);
    }

    public void HideResult() {
      HideStatus();
    }
  }
}
