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
// Three phases, matching the event lifecycle:
//   OnConfirmed<T> - the event is authoritative (verified). Routes both OnSyncedEvent (for
//                    EventMode.Synced events) and OnEventConfirmed (for Regular events), so a
//                    listener gets "T definitely happened" exactly once regardless of the event's
//                    mode. Default for UI: no rollback flicker.
//   OnPredicted<T> - fired on a predicted tick (Regular events only). Use for responsive,
//                    transient feedback that is fine to occasionally mispredict.
//   OnCanceled<T>  - a previously predicted event was rolled back. Use to undo OnPredicted feedback.
public class SimEventHub {
  private readonly Dictionary<Type, List<Action<SimulationEvent>>> _canceled = new();
  private readonly Dictionary<Type, List<Action<SimulationEvent>>> _confirmed = new();
  private readonly Dictionary<Type, List<Action<SimulationEvent>>> _predicted = new();

  private IKlothoEngine _engine;
  private Action<int, SimulationEvent> _onCanceled;
  private Action<int, SimulationEvent> _onConfirmed;
  private Action<int, SimulationEvent> _onPredicted;
  private Action<int, SimulationEvent> _onSynced;

  public void Attach(IKlothoEngine engine) {
    Detach();
    _engine = engine;
    _onPredicted = (_, evt) => Dispatch(_predicted, evt);
    _onConfirmed = (_, evt) => Dispatch(_confirmed, evt);
    _onSynced = (_, evt) => Dispatch(_confirmed, evt);
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

  private static void Dispatch(Dictionary<Type, List<Action<SimulationEvent>>> map, SimulationEvent evt) {
    if (!map.TryGetValue(evt.GetType(), out var list)) return;
    // Snapshot: a handler may unsubscribe (and thus mutate the list) while we iterate.
    for (var i = 0; i < list.Count; i++)
      list[i](evt);
  }

  private sealed class Subscription : IDisposable {
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
