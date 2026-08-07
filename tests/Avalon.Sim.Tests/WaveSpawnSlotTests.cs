using System.Collections.Generic;
using FluentAssertions;
using Meesles.Avalon;
using Xunit;
using xpTURN.Klotho.Deterministic.Math;

namespace Meesles.Avalon.Sim.Tests;

public class WaveSpawnSlotTests {
  private static readonly FPVector3 Origin = new(FP64.FromInt(7), FP64.Zero, FP64.FromInt(-3));
  private static readonly FP64 Spacing = FP64.FromDouble(0.8);

  [Fact]
  public void Cursor_StartsOnTheSpawnPoint() {
    var cursor = default(WaveSpawnSystem.SlotCursor);

    cursor.GetPosition(Origin, Spacing).Should().Be(Origin);
    cursor.AtLastSlot.Should().BeFalse();
  }

  [Fact]
  public void Cursor_WalksSixSlotsPerRingAtRingRadius() {
    var cursor = default(WaveSpawnSystem.SlotCursor);

    for (var ring = 1; ring <= WaveSpawnSystem.MaxRing; ring++) {
      for (var slot = 0; slot < 6 * ring; slot++) {
        cursor.Advance();
        float radius = (cursor.GetPosition(Origin, Spacing) - Origin).magnitude.ToFloat();
        radius.Should().BeApproximately(ring * Spacing.ToFloat(), 0.01f);
      }
    }

    cursor.AtLastSlot.Should().BeTrue();
  }

  // The occupancy test treats a slot as taken when a minion sits within half a spacing of it, so
  // distinct slots have to stay at least a spacing apart or the search can never fill them.
  [Fact]
  public void Cursor_SlotsAreAtLeastOneSpacingApart() {
    var positions = new List<FPVector3>();
    var cursor = default(WaveSpawnSystem.SlotCursor);

    positions.Add(cursor.GetPosition(Origin, Spacing));
    while (!cursor.AtLastSlot) {
      cursor.Advance();
      positions.Add(cursor.GetPosition(Origin, Spacing));
    }

    positions.Should().HaveCount(3 * WaveSpawnSystem.MaxRing * (WaveSpawnSystem.MaxRing + 1) + 1);

    float minSpacing = Spacing.ToFloat() - 0.01f;
    for (var i = 0; i < positions.Count; i++)
      for (var j = i + 1; j < positions.Count; j++)
        (positions[i] - positions[j]).magnitude.ToFloat().Should().BeGreaterThan(minSpacing);
  }

  [Fact]
  public void Cursor_StopsOnTheOuterRingInsteadOfSprawling() {
    var cursor = default(WaveSpawnSystem.SlotCursor);
    while (!cursor.AtLastSlot)
      cursor.Advance();

    var saturated = cursor.GetPosition(Origin, Spacing);
    for (var i = 0; i < 1000; i++)
      cursor.Advance();

    cursor.GetPosition(Origin, Spacing).Should().Be(saturated);
    (saturated - Origin).magnitude.ToFloat()
      .Should().BeApproximately(WaveSpawnSystem.MaxRing * Spacing.ToFloat(), 0.01f);
  }
}
