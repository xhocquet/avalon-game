// In-game HUD: state / score P1,P2 / match timer / result.
using global::Godot;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Network;

namespace Meesles.Avalon
{
  public partial class Hud : Control
  {
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
    }

    public void SetPhase(SessionPhase phase)
    {
      _state.Text = phase.ToString();
    }

    public void SetLocalReady(bool ready)
    {
      _state.Text = ready ? "Ready" : _state.Text;
    }

    public void SyncFromFrame(Frame frame)
    {
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
      _resultPanel.Visible = true;
      _resultLabel.Text = text;
    }

    public void HideResult()
    {
      _resultPanel.Visible = false;
    }
  }
}
