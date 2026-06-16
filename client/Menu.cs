// Session-control menu: Join/Ready/Stop buttons + IP/Port.
using System;
using global::Godot;

namespace Meesles.Avalon
{
  public partial class Menu : Control
  {
    private Button _joinButton;
    private Button _readyButton;
    private Button _stopButton;
    private LineEdit _ipField;
    private LineEdit _portField;

    public event Action OnJoinClicked;
    public event Action OnReadyClicked;
    public event Action OnStopClicked;

    public string Host => _ipField?.Text?.Trim();
    public int Port => int.TryParse(_portField?.Text, out int p) ? p : 7777;

    public override void _Ready()
    {
      _joinButton = GetNode<Button>("VBox/JoinButton");
      _readyButton = GetNode<Button>("VBox/ReadyButton");
      _stopButton = GetNode<Button>("VBox/StopButton");
      _ipField = GetNode<LineEdit>("VBox/IpField");
      _portField = GetNode<LineEdit>("VBox/PortField");

      _joinButton.Pressed += () => OnJoinClicked?.Invoke();
      _readyButton.Pressed += () => OnReadyClicked?.Invoke();
      _stopButton.Pressed += () => OnStopClicked?.Invoke();
    }

    public void SetInitialHost(string host, int port)
    {
      if (_ipField != null) _ipField.Text = host;
      if (_portField != null) _portField.Text = port.ToString();
    }

    public void SetReadyEnabled(bool enabled)
    {
      if (_readyButton != null) _readyButton.Disabled = !enabled;
    }

    public void SetStopEnabled(bool enabled)
    {
      if (_stopButton != null) _stopButton.Disabled = !enabled;
    }
  }
}
