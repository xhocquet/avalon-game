using System;
using Godot;

namespace Meesles.Avalon {
  public partial class Menu : Control {
    private bool _singleplayerMode;
    private bool _isReady;
    private Button _joinButton;
    private Button _readyButton;
    private Button _stopButton;
    private LineEdit _ipField;
    private LineEdit _portField;

    public event Action OnResetClicked;
    public event Action OnJoinClicked;
    public event Action OnReadyClicked;
    public event Action OnUnreadyClicked;
    public event Action OnStopClicked;

    public string Host => _ipField?.Text?.Trim();
    public int Port => int.TryParse(_portField?.Text, out int p) ? p : 7777;

    public override void _Ready() {
      _joinButton = GetNode<Button>("VBox/JoinButton");
      _readyButton = GetNode<Button>("VBox/ReadyButton");
      _stopButton = GetNode<Button>("VBox/StopButton");
      _ipField = GetNode<LineEdit>("VBox/IpField");
      _portField = GetNode<LineEdit>("VBox/PortField");

      _joinButton.Pressed += HandleJoinPressed;
      _readyButton.Pressed += HandleReadyPressed;
      _stopButton.Pressed += () => OnStopClicked?.Invoke();
    }

    public void SetSingleplayerMode() {
      _singleplayerMode = true;
      _joinButton.Text = "Reset Sandbox";
      _readyButton.Visible = false;
      _stopButton.Visible = false;
      _ipField.Visible = false;
      _portField.Visible = false;
      OnJoinClicked = null;
    }

    public void SetLobbyMode() {
      _singleplayerMode = false;
      _joinButton.Text = "Join";
      _readyButton.Visible = true;
      _stopButton.Visible = true;
      _ipField.Visible = true;
      _portField.Visible = true;
    }

    public void SetGameMode() {
      Visible = false;
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

    private void HandleJoinPressed() {
      if (_singleplayerMode) OnResetClicked?.Invoke();
      else OnJoinClicked?.Invoke();
    }

    private void HandleReadyPressed() {
      if (_isReady) OnUnreadyClicked?.Invoke();
      else OnReadyClicked?.Invoke();
    }
  }
}
