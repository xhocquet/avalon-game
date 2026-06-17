using global::Godot;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Network;

namespace Meesles.Avalon
{
  public partial class Hud : Control
  {
    private bool _sandboxMode = true;
    private Label _state;
    private Label _score0;
    private Label _score1;
    private Label _timer;
    private Panel _resultPanel;
    private Label _resultLabel;

    public override void _Ready()
    {
      _state = GetNode<Label>("StateLabel");
      _score0 = GetNode<Label>("Score0Label");
      _score1 = GetNode<Label>("Score1Label");
      _timer = GetNode<Label>("TimerLabel");
      _resultPanel = GetNode<Panel>("ResultPanel");
      _resultLabel = GetNode<Label>("ResultPanel/ResultLabel");
      _resultPanel.Visible = false;
      if (_sandboxMode) SetSandboxMode();
    }

    public void SetSandboxMode()
    {
      _sandboxMode = true;
      _state.Text = "Local Sandbox";
      _score0.Text = "Move: WASD or arrow keys";
      _score1.Text = "Reset: button on the left";
      _timer.Text = "Idle";
    }

    public void SetMultiplayerMode()
    {
      _sandboxMode = false;
      _state.Text = "Disconnected";
      _score0.Text = "P1: 0";
      _score1.Text = "P2: 0";
      _timer.Text = "0s";
      HideStatus();
    }

    public void SyncSandbox(Vector3 position, Vector3 movement)
    {
      if (!_sandboxMode) return;
      _score1.Text = $"Position: {position.X:0.0}, {position.Z:0.0}";
      _timer.Text = movement.LengthSquared() > 0.001f ? "Moving" : "Idle";
    }

    public void ShowStatus(string text)
    {
      _resultPanel.Visible = true;
      _resultLabel.Text = text;
    }

    public void HideStatus()
    {
      _resultPanel.Visible = false;
    }

    public void SetPhase(SessionPhase _)
    {
      if (_sandboxMode) return;
      _state.Text = _.ToString();
    }

    public void SetLocalReady(bool ready)
    {
      if (_sandboxMode) _timer.Text = ready ? "Ready" : "Idle";
      else _state.Text = ready ? "Ready" : _state.Text;
    }

    public void SyncFromFrame(Frame frame)
    {
      if (_sandboxMode) return;

      int p1 = 0;
      int p2 = 0;
      var filter = frame.Filter<PlayerComponent>();
      while (filter.Next(out var entity))
      {
        ref readonly var player = ref frame.GetReadOnly<PlayerComponent>(entity);
        if (player.PlayerId == 1) p1 = player.Score;
        else if (player.PlayerId == 2) p2 = player.Score;
      }

      _score0.Text = $"P1: {p1}";
      _score1.Text = $"P2: {p2}";

      double remaining = frame.AssetRegistry.Get<PlayerStatsAsset>().MatchDuration.ToDouble()
          - ((frame.Tick * frame.DeltaTimeMs) / 1000.0);
      if (remaining < 0) remaining = 0;
      _timer.Text = $"{remaining:0.0}s";
    }

    public void ShowResult(string text)
    {
      ShowStatus(text);
    }

    public void HideResult()
    {
      HideStatus();
    }
  }
}
