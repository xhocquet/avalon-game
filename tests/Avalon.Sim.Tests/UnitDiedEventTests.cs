using FluentAssertions;
using Xunit;
using xpTURN.Klotho.Deterministic.Math;

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
}
