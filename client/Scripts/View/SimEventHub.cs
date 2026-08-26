using System;
using System.Collections.Generic;
using xpTURN.Klotho.Core;

namespace Meesles.Avalon;

// Routes the engine's simulation-event streams to typed, per-listener handlers so many UI
// pieces (GameUI, kill feed, minimap, ...) can each subscribe to just the events they render.
//
// Lifecycle mirrors VfxManager: the owning GameNode calls Attach(engine) when a session starts
// and Detach() when it stops. Listener subscriptions (On*<T>) persist across Attach/Detach, so a
// node can subscribe once in _Ready and keep reacting across session restarts; Detach only tears
// down the engine wiring.
//
// Four phases, matching the event lifecycle:
//   OnFx<T>        - fired once per event, on whichever of the two Regular streams reaches it first.
//                    The stream to use for VFX, animation and audio: see the note below.
//   OnConfirmed<T> - the event is authoritative (verified). Routes both OnSyncedEvent (for
//                    EventMode.Synced events) and OnEventConfirmed (for Regular events), so a
//                    listener gets "T definitely happened" exactly once regardless of the event's
//                    mode. Default for UI: no rollback flicker.
//   OnPredicted<T> - fired on a predicted tick (Regular events only). Use for responsive,
//                    transient feedback that is fine to occasionally mispredict.
//   OnCanceled<T>  - a previously predicted event was rolled back. Use to undo OnFx/OnPredicted work.
//
// Why OnFx exists: which of OnPredicted/OnConfirmed a Regular event arrives on is a property of the
// session, not of the event. A networked client runs ahead of the server, so its ticks are Predicted
// and the event fires there. A local host has every input in hand, so KlothoEngine takes the
// non-predicting path, marks every tick Verified, and the same event fires on Confirmed instead -
// which is why VFX subscribed to OnPredicted alone were silently dead in singleplayer and the
// playgrounds. OnFx takes both and de-duplicates, so client-side feedback plays in either.
public class SimEventHub {
  // How many ticks of dispatched-event keys to remember. Only has to outlive the window in which the
  // same event could reach both streams, which is one rollback at most.
  private const int FxDedupeTicks = 256;

  private readonly Dictionary<Type, List<Action<SimulationEvent>>> _canceled = new();
  private readonly Dictionary<Type, List<Action<SimulationEvent>>> _confirmed = new();
  private readonly Dictionary<Type, List<Action<SimulationEvent>>> _fx = new();
  private readonly Dictionary<Type, List<Action<SimulationEvent>>> _predicted = new();

  // Identity of an event already handed to the fx stream, keyed the same way Klotho's own rollback
  // comparison keys one: same tick, same type, same content is the same event.
  private readonly HashSet<(int Tick, int TypeId, long ContentHash)> _fxDispatched = new();
  private int _fxPrunedThroughTick;

  private IKlothoEngine _engine;
  private Action<int, SimulationEvent> _onCanceled;
  private Action<int, SimulationEvent> _onConfirmed;
  private Action<int, SimulationEvent> _onPredicted;
  private Action<int, SimulationEvent> _onSynced;

  public void Attach(IKlothoEngine engine) {
    Detach();
    _engine = engine;
    _onPredicted = (_, evt) => {
      DispatchFx(evt);
      Dispatch(_predicted, evt);
    };
    _onConfirmed = (_, evt) => {
      DispatchFx(evt);
      Dispatch(_confirmed, evt);
    };
    _onSynced = (_, evt) => {
      DispatchFx(evt);
      Dispatch(_confirmed, evt);
    };
    _onCanceled = (_, evt) => Dispatch(_canceled, evt);
    _engine.OnEventPredicted += _onPredicted;
    _engine.OnEventConfirmed += _onConfirmed;
    _engine.OnSyncedEvent += _onSynced;
    _engine.OnEventCanceled += _onCanceled;
  }

  public void Detach() {
    if (_engine != null) {
      _engine.OnEventPredicted -= _onPredicted;
      _engine.OnEventConfirmed -= _onConfirmed;
      _engine.OnSyncedEvent -= _onSynced;
      _engine.OnEventCanceled -= _onCanceled;
    }

    _engine = null;
    _onPredicted = _onConfirmed = _onSynced = _onCanceled = null;
    _fxDispatched.Clear();
    _fxPrunedThroughTick = 0;
  }

  public IDisposable OnFx<T>(Action<T> handler) where T : SimulationEvent {
    return Add(_fx, handler);
  }

  public IDisposable OnConfirmed<T>(Action<T> handler) where T : SimulationEvent {
    return Add(_confirmed, handler);
  }

  public IDisposable OnPredicted<T>(Action<T> handler) where T : SimulationEvent {
    return Add(_predicted, handler);
  }

  public IDisposable OnCanceled<T>(Action<T> handler) where T : SimulationEvent {
    return Add(_canceled, handler);
  }

  private static IDisposable Add<T>(Dictionary<Type, List<Action<SimulationEvent>>> map, Action<T> handler)
    where T : SimulationEvent {
    if (handler == null) throw new ArgumentNullException(nameof(handler));
    if (!map.TryGetValue(typeof(T), out var list)) {
      list = new List<Action<SimulationEvent>>();
      map[typeof(T)] = list;
    }

    Action<SimulationEvent> wrapper = evt => handler((T)evt);
    list.Add(wrapper);
    return new Subscription(list, wrapper);
  }

  // Drops the second arrival of an event that reached both streams, which is what a rollback that
  // re-confirms an already-predicted event looks like. Cheap enough to run for every event: with no
  // fx listeners registered for the type there is nothing to remember.
  private void DispatchFx(SimulationEvent evt) {
    if (!_fx.ContainsKey(evt.GetType())) return;

    if (!_fxDispatched.Add((evt.Tick, evt.EventTypeId, evt.GetContentHash())))
      return;

    PruneFxKeys(evt.Tick);
    Dispatch(_fx, evt);
  }

  private void PruneFxKeys(int tick) {
    var cutoff = tick - FxDedupeTicks;
    if (cutoff <= _fxPrunedThroughTick) return;

    _fxDispatched.RemoveWhere(key => key.Tick <= cutoff);
    _fxPrunedThroughTick = cutoff;
  }

  private static void Dispatch(Dictionary<Type, List<Action<SimulationEvent>>> map, SimulationEvent evt) {
    if (!map.TryGetValue(evt.GetType(), out var list)) return;
    // Snapshot: a handler may unsubscribe (and thus mutate the list) while we iterate.
    for (var i = 0; i < list.Count; i++)
      list[i](evt);
  }

  private class Subscription : IDisposable {
    private List<Action<SimulationEvent>> _list;
    private Action<SimulationEvent> _wrapper;

    public Subscription(List<Action<SimulationEvent>> list, Action<SimulationEvent> wrapper) {
      _list = list;
      _wrapper = wrapper;
    }

    public void Dispose() {
      _list?.Remove(_wrapper);
      _list = null;
      _wrapper = null;
    }
  }
}
