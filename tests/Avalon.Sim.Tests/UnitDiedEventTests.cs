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
      Position = new FPVector3(FP64.One, FP64.Zero, -FP64.One),
    };

    evt.Reset();

    evt.UnitId.Should().Be(0);
    evt.UnitTypeId.Should().Be(0);
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
      OwnerId = 3,
      DestroyerUnitId = 20,
      DestroyerTeamId = 4,
      DestroyerOwnerId = 5,
    };

    evt.Reset();

    evt.UnitId.Should().Be(0);
    evt.CrystalId.Should().Be(0);
    evt.TeamId.Should().Be(0);
    evt.OwnerId.Should().Be(0);
    evt.DestroyerUnitId.Should().Be(0);
    evt.DestroyerTeamId.Should().Be(0);
    evt.DestroyerOwnerId.Should().Be(0);
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
  public void GameOverEvent_UsesReservedTypeSyncedModeAndNoPayload() {
    var evt = new GameOverEvent();
    var matchEnd = (IMatchEndEvent)evt;

    evt.EventTypeId.Should().Be(101);
    evt.Mode.Should().Be(EventMode.Synced);
    matchEnd.WinnerPlayerId.Should().Be(-1);
    matchEnd.Reason.Should().Be(default(FixedString32));
  }
}
