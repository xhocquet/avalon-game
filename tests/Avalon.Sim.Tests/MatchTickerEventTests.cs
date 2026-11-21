using System.Linq;
using FluentAssertions;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.ECS;
using Xunit;

namespace Meesles.Avalon.Sim.Tests;

public class MatchTickerEventTests {
  [Fact]
  public void PlayerDeath_CapturesBothChampionsAndFatalAttacker() {
    var frame = SimHarness.CreateInitialized().Frame;
    var victim = FindHero(ref frame, 1);
    var killer = FindHero(ref frame, 2);
    frame.Get<Health>(victim).Current = 0;
    frame.Get<Health>(victim).LastDamagerUnitId = UnitLookup.GetUnitId(ref frame, killer);
    var collector = new EventCollector();
    collector.BeginTick(12);
    frame.EventRaiser = collector;

    new RespawnSystem().Update(ref frame);

    var evt = collector.Collected.OfType<PlayerDiedEvent>().Single();
    evt.PlayerId.Should().Be(1);
    evt.FactionId.Should().Be(frame.GetReadOnly<Faction>(victim).FactionId);
    evt.DestroyerPlayerId.Should().Be(2);
    evt.DestroyerFactionId.Should().Be(frame.GetReadOnly<Faction>(killer).FactionId);
    evt.DestroyerTeamId.Should().Be(frame.GetReadOnly<Team>(killer).TeamId);
    evt.DestroyerUnitId.Should().Be(UnitLookup.GetUnitId(ref frame, killer));
    evt.Reset();
    evt.FactionId.Should().Be(0);
    evt.DestroyerFactionId.Should().Be(0);
    evt.DestroyerPlayerId.Should().Be(0);
  }

  [Fact]
  public void StructureDeaths_CaptureAttackerBeforeEntitiesAreDestroyed() {
    var frame = SimHarness.CreateInitialized().Frame;
    var killer = FindHero(ref frame, 2);
    var owner = FindHero(ref frame, 1);
    frame.Get<Faction>(owner).FactionId = Assets.AssetIds.FactionSnailheads;
    var ownerFactionId = frame.GetReadOnly<Faction>(owner).FactionId;
    var killerFactionId = frame.GetReadOnly<Faction>(killer).FactionId;
    var killerTeamId = frame.GetReadOnly<Team>(killer).TeamId;
    var killerUnitId = UnitLookup.GetUnitId(ref frame, killer);
    var structures = frame.Filter<UnitIdentity, Health, Team>();
    while (structures.Next(out var entity)) {
      if (frame.GetReadOnly<Team>(entity).TeamId != 1 ||
          (!frame.Has<Turret>(entity) && !frame.Has<Crystal>(entity))) continue;
      frame.Get<Health>(entity).Current = 0;
      frame.Get<Health>(entity).LastDamagerUnitId = killerUnitId;
    }
    var collector = new EventCollector();
    collector.BeginTick(20);
    frame.EventRaiser = collector;

    new DeathSystem().Update(ref frame);

    var turrets = collector.Collected.OfType<TurretDestroyedEvent>().ToArray();
    var crystals = collector.Collected.OfType<CrystalDestroyedEvent>().ToArray();
    turrets.Should().NotBeEmpty();
    crystals.Should().NotBeEmpty();
    foreach (var evt in turrets) {
      evt.PlayerId.Should().Be(1);
      evt.FactionId.Should().Be(ownerFactionId);
      evt.TeamId.Should().Be(1);
      evt.DestroyerPlayerId.Should().Be(2);
      evt.DestroyerFactionId.Should().Be(killerFactionId);
      evt.DestroyerTeamId.Should().Be(killerTeamId);
    }
    foreach (var evt in crystals) {
      evt.PlayerId.Should().Be(1);
      evt.FactionId.Should().Be(ownerFactionId);
      evt.DestroyerPlayerId.Should().Be(2);
      evt.DestroyerFactionId.Should().Be(killerFactionId);
    }
  }

  private static EntityRef FindHero(ref Frame frame, int playerId) {
    var heroes = frame.Filter<Hero>();
    while (heroes.Next(out var entity))
      if (frame.GetReadOnly<Hero>(entity).PlayerId == playerId) return entity;
    throw new System.InvalidOperationException();
  }
}
