using System;
using System.Collections.Generic;
using System.Reflection;
using GdUnit4;
using Meesles.Avalon.Sim;
using xpTURN.Klotho.Core;
using static GdUnit4.Assertions;

namespace Meesles.Avalon.Client.Tests;

[TestSuite]
public class MatchTimelineTests {
  [TestCase]
  public void Timeline_CopiesConfirmedParticipantsAndIgnoresRespawns() {
    var engine = DispatchProxy.Create<IKlothoEngine, TimelineEngineProxy>();
    var proxy = (TimelineEngineProxy)(object)engine;
    var hub = new SimEventHub();
    hub.Timeline.ResolvePlayerName = id => id == 1 ? "SoupWizard" : "SirBonks";
    hub.Attach(engine);
    var evt = new PlayerDiedEvent {
      Tick = 10, PlayerId = 1, TeamId = 1, FactionId = FactionCatalog.FactionSnailheadsId,
      DestroyerPlayerId = 2, DestroyerTeamId = 2, DestroyerFactionId = FactionCatalog.FactionPickleKnightsId
    };
    proxy.Emit("OnEventPredicted", evt);
    proxy.Emit("OnSyncedEvent", new PlayerRespawnedEvent());
    AssertThat(hub.Timeline.Entries.Count).IsEqual(0);
    proxy.Emit("OnSyncedEvent", evt);
    evt.FactionId = 0;
    hub.Timeline.ResolvePlayerName = _ => "Renamed";

    var entry = hub.Timeline.Entries[0];
    var message = MatchTickerTemplates.Resolve(entry);
    AssertThat(message.Actor.Name).IsEqual("SirBonks");
    AssertThat(message.Target.Name).IsEqual("SoupWizard");
    AssertThat(message.Target.FactionId).IsEqual(FactionCatalog.FactionSnailheadsId);
    AssertThat(message.Text).IsEqual("defeated");
    hub.Detach();
    AssertThat(hub.Timeline.Entries.Count).IsEqual(0);
    proxy.Emit("OnSyncedEvent", evt);
    AssertThat(hub.Timeline.Entries.Count).IsEqual(0);
  }

  [TestCase]
  public void Templates_HandleUncreditedDeathsAndStructures() {
    var hero = new MatchTimelineParticipant(1, "SoupWizard", FactionCatalog.FactionSnailheadsId, 1);
    var death = MatchTickerTemplates.Resolve(new(1, 1, MatchTimelineKind.PlayerDefeated, Target: hero));
    AssertThat(death.Actor.Name).IsEqual("SoupWizard");
    AssertThat(death.Text).IsEqual("defeated!");
    AssertThat(death.Target.IsHero).IsFalse();
    var turret = MatchTickerTemplates.Resolve(new(2, 1, MatchTimelineKind.TurretDestroyed, Target: hero));
    AssertThat(turret.Actor.FactionId).IsEqual(hero.FactionId);
    AssertThat(turret.Actor.Name).IsEqual("SoupWizard's");
    AssertThat(turret.Text).IsEqual("turret destroyed.");
    var uncredited = MatchTickerTemplates.Resolve(new(3, 1, MatchTimelineKind.TurretDestroyed));
    AssertThat(uncredited.Actor.IsHero).IsFalse();
    AssertThat(uncredited.Text).IsEqual("Turret destroyed.");
  }

  [TestCase]
  public void StructureTemplates_UseOwnerInsteadOfAttackerChampion() {
    var engine = DispatchProxy.Create<IKlothoEngine, TimelineEngineProxy>();
    var proxy = (TimelineEngineProxy)(object)engine;
    var hub = new SimEventHub();
    hub.Attach(engine);
    hub.Timeline.ResolvePlayerName = id => id == 1 ? "SoupWizard" : "SirBonks";
    proxy.Emit("OnSyncedEvent", new TurretDestroyedEvent {
      PlayerId = 1, TeamId = 1, FactionId = FactionCatalog.FactionSnailheadsId,
      DestroyerPlayerId = 2, DestroyerTeamId = 2, DestroyerFactionId = FactionCatalog.FactionPickleKnightsId
    });
    proxy.Emit("OnSyncedEvent", new CrystalDestroyedEvent {
      PlayerId = 1, TeamId = 1, FactionId = FactionCatalog.FactionSnailheadsId,
      DestroyerPlayerId = -1
    });
    foreach (var entry in hub.Timeline.Entries) {
      var message = MatchTickerTemplates.Resolve(entry);
      AssertThat(message.Actor.Name).IsEqual("SoupWizard's");
      AssertThat(message.Actor.FactionId).IsEqual(FactionCatalog.FactionSnailheadsId);
      AssertThat(message.Actor.TeamId).IsEqual(1);
      AssertThat(message.Target.IsHero).IsFalse();
    }
    AssertThat(MatchTickerTemplates.Resolve(hub.Timeline.Entries[1]).Text).IsEqual("base destroyed.");
    hub.Detach();
  }
}

public class TimelineEngineProxy : DispatchProxy {
  private readonly Dictionary<string, Delegate> _handlers = new();
  protected override object Invoke(MethodInfo method, object[] args) {
    var add = method.Name.StartsWith("add_");
    var name = method.Name[(add ? 4 : 7)..];
    _handlers.TryGetValue(name, out var previous);
    _handlers[name] = add ? Delegate.Combine(previous, (Delegate)args[0]) : Delegate.Remove(previous, (Delegate)args[0]);
    return null;
  }
  public void Emit(string name, SimulationEvent evt) {
    if (_handlers.TryGetValue(name, out var handler)) handler?.DynamicInvoke(evt.Tick, evt);
  }
}
