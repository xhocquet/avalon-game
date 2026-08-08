using System;
using Meesles.Avalon.Sim;
using xpTURN.Klotho.Core;

namespace Meesles.Avalon.Client.Scripts.View;

public class ViewCallbacks : IViewCallbacks {
  private IKlothoEngine _engine;
  private Action<int, SimulationEvent> _eventConfirmedHandler;
  private bool _gameOverShown;
  private IViewHud _hud;

  public ViewCallbacks(IViewHud hud) {
    _hud = hud;
  }

  public void OnGameStart(IKlothoEngine engine) {
    AttachEngine(engine);
  }

  public void OnLateJoinActivated(IKlothoEngine engine) {
    AttachEngine(engine);
  }

  public void OnTickExecuted(int tick) {
    if (_engine == null || _engine.PredictedFrame.Frame == null) return;
    _hud?.SyncFromFrame(_engine.PredictedFrame.Frame);

    // Backstop for the paths that never deliver the event: a resync/FullState restore past the end
    // tick, or a client that joined after it. The state is what decides, not the notification.
    if (!_gameOverShown && _engine.IsMatchEnded) {
      _gameOverShown = true;
      ShowResult();
    }
  }

  public void SetHud(IViewHud hud) {
    _hud = hud;
    if (_engine != null)
      _hud.SetLocalPlayerId(_engine.LocalPlayerId >= 0 ? _engine.LocalPlayerId : null);
  }

  public void OnSessionCreated(IKlothoSession session) {
    _engine = session.Engine;
    _gameOverShown = false;
    _hud?.SetLocalPlayerId(_engine != null && _engine.LocalPlayerId >= 0 ? _engine.LocalPlayerId : null);
    _hud?.HideResult();
  }

  public void OnSessionStopped() {
    DetachEngine();
    _engine = null;
    _gameOverShown = false;
    _hud?.SetLocalPlayerId(null);
    _hud?.HideResult();
  }

  public void Cleanup() {
    DetachEngine();
    _engine = null;
    _gameOverShown = false;
    _hud?.SetLocalPlayerId(null);
    _hud?.HideResult();
  }

  private void AttachEngine(IKlothoEngine engine) {
    if (ReferenceEquals(_engine, engine) && _eventConfirmedHandler != null) return;

    DetachEngine();
    _engine = engine;
    _eventConfirmedHandler = (tick, evt) => {
      if (_gameOverShown) return;
      if (evt is not GameOverEvent) return;

      _gameOverShown = true;
      ShowResult();
    };
    _engine.OnEventConfirmed += _eventConfirmedHandler;
  }

  private void DetachEngine() {
    if (_engine != null && _eventConfirmedHandler != null)
      _engine.OnEventConfirmed -= _eventConfirmedHandler;
    _eventConfirmedHandler = null;
  }

  // The outcome and every scoreboard row come off the frame; the event only says the match is over.
  // The verified frame is preferred so the numbers shown are the ones that stuck.
  private void ShowResult() {
    var frame = _engine?.VerifiedFrame.Frame ?? _engine?.PredictedFrame.Frame;
    if (frame == null || !MatchResultReader.TryRead(ref frame, out var result))
      return;

    _hud?.ShowResult(result);
  }
}
