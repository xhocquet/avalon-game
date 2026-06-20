// Game view callbacks: drives the HUD from engine state.
using System;
using xpTURN.Klotho.Core;

namespace Meesles.Avalon {
  public class ViewCallbacks : IViewCallbacks {
    private LobbyUI _hud;
    private IKlothoEngine _engine;
    private bool _gameOverShown;
    private Action<int, SimulationEvent> _eventConfirmedHandler;

    public ViewCallbacks(LobbyUI hud) {
      _hud = hud;
    }

    public void SetLobbyUI(LobbyUI hud) {
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

    public void OnGameStart(IKlothoEngine engine) {
      AttachEngine(engine);
    }

    public void OnLateJoinActivated(IKlothoEngine engine) {
      AttachEngine(engine);
    }

    public void OnTickExecuted(int tick) {
      if (_engine == null || _engine.PredictedFrame.Frame == null) return;
      _hud?.SyncFromFrame(_engine.PredictedFrame.Frame);
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
      _eventConfirmedHandler = (_, evt) => {
        if (_gameOverShown) return;
        if (evt is not GameOverEvent gameOver) return;

        _gameOverShown = true;
        _hud?.ShowResult(gameOver.WinnerPlayerId switch {
          0 => "P1 Wins",
          1 => "P1 Wins",
          2 => "P2 Wins",
          _ => "Draw",
        });
      };
      _engine.OnEventConfirmed += _eventConfirmedHandler;
    }

    private void DetachEngine() {
      if (_engine != null && _eventConfirmedHandler != null)
        _engine.OnEventConfirmed -= _eventConfirmedHandler;
      _eventConfirmedHandler = null;
    }
  }
}
