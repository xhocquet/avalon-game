using FluentAssertions;
using Xunit;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Tests;

public class UnitDiedEventTests {
  [Fact]
  public void UnitDiedEvent_UsesReservedTypeAndSyncedMode() {
    var evt = new UnitDiedEvent();

    evt.EventTypeId.Should().Be(102);
    evt.Mode.Should().Be(xpTURN.Klotho.Core.EventMode.Synced);
  }

  [Fact]
  public void Reset_ClearsPayloadFields() {
    var evt = new UnitDiedEvent {
      UnitId = 10,
      UnitTypeId = 2,
      DestroyerUnitId = 20,
      DestroyerUnitTypeId = 1,
      Position = new FPVector3(FP64.One, FP64.Zero, -FP64.One),
    };

    evt.Reset();

    evt.UnitId.Should().Be(0);
    evt.UnitTypeId.Should().Be(0);
    evt.DestroyerUnitId.Should().Be(0);
    evt.DestroyerUnitTypeId.Should().Be(0);
    evt.Position.x.Should().Be(FP64.Zero);
    evt.Position.y.Should().Be(FP64.Zero);
    evt.Position.z.Should().Be(FP64.Zero);
  }

  [Fact]
  public void CrystalDestroyedEvent_UsesReservedTypeAndSyncedMode() {
    var evt = new CrystalDestroyedEvent();

    evt.EventTypeId.Should().Be(106);
    evt.Mode.Should().Be(EventMode.Synced);
  }

  [Fact]
  public void CrystalDestroyedEvent_ResetClearsUnitId() {
    var evt = new CrystalDestroyedEvent {
      UnitId = 10,
      CrystalId = 1,
      TeamId = 2,
      DestroyerUnitId = 20,
      DestroyerTeamId = 4,
    };

    evt.Reset();

    evt.UnitId.Should().Be(0);
    evt.CrystalId.Should().Be(0);
    evt.TeamId.Should().Be(0);
    evt.DestroyerUnitId.Should().Be(0);
    evt.DestroyerTeamId.Should().Be(0);
  }

  [Fact]
  public void TurretDestroyedEvent_UsesReservedTypeAndSyncedMode() {
    var evt = new TurretDestroyedEvent();

    evt.EventTypeId.Should().Be(107);
    evt.Mode.Should().Be(EventMode.Synced);
  }

  [Fact]
  public void TurretDestroyedEvent_ResetClearsUnitId() {
    var evt = new TurretDestroyedEvent {
      UnitId = 10,
      DestroyerUnitId = 20,
    };

    evt.Reset();

    evt.UnitId.Should().Be(0);
    evt.DestroyerUnitId.Should().Be(0);
  }

  [Fact]
  public void GameOverEvent_UsesReservedTypeAndSyncedMode() {
    var evt = new GameOverEvent();

    evt.EventTypeId.Should().Be(101);
    evt.Mode.Should().Be(EventMode.Synced);
  }

  [Fact]
  public void GameOverEvent_ExposesItsOutcomeThroughIMatchEndEvent() {
    var evt = new GameOverEvent { WinnerPlayerId = 2, WinnerTeamId = 2, Reason = (int)MatchEndReason.Crystal };
    var matchEnd = (IMatchEndEvent)evt;

    matchEnd.WinnerPlayerId.Should().Be(2);
    matchEnd.Reason.Should().Be(FixedString32.FromString("crystal"));

    // The payload is what makes two different outcomes hash differently on the wire.
    var timeout = new GameOverEvent { WinnerPlayerId = -1, WinnerTeamId = -1, Reason = (int)MatchEndReason.Timeout };
    timeout.GetContentHash().Should().NotBe(evt.GetContentHash());
  }

  [Fact]
  public void GameOverEvent_ResetClearsTheOutcomeForThePool() {
    var evt = new GameOverEvent { WinnerPlayerId = 2, WinnerTeamId = 2, Reason = (int)MatchEndReason.Crystal };

    evt.Reset();

    evt.WinnerPlayerId.Should().Be(0);
    evt.WinnerTeamId.Should().Be(0);
    evt.Reason.Should().Be((int)MatchEndReason.Unknown);
  }
}
