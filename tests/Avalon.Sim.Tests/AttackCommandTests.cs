using FluentAssertions;
using Meesles.Avalon.Sim.Commands;
using Xunit;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim.Tests;

public class AttackCommandTests {
  [Fact]
  public void SourceUnitIds_GrowPastInitialCapacity() {
    var command = new AttackCommand { TargetUnitId = 99 };

    for (int i = 0; i < 12; i++)
      command.UnitIds.Add(i + 1);

    command.UnitIds.Count.Should().Be(12);
    for (int i = 0; i < command.UnitIds.Count; i++)
      command.UnitIds[i].Should().Be(i + 1);
  }

  [Fact]
  public void Serialize_RoundTripsTargetAndSourceUnitIds() {
    var original = new AttackCommand {
      PlayerId = 2,
      Tick = 17,
      TargetUnitId = 44,
    };
    original.UnitIds.Add(10);
    original.UnitIds.Add(11);
    original.UnitIds.Add(12);

    var restored = RoundTrip(original);

    restored.PlayerId.Should().Be(2);
    restored.Tick.Should().Be(17);
    restored.TargetUnitId.Should().Be(44);
    restored.UnitIds.Count.Should().Be(3);
    restored.UnitIds[0].Should().Be(10);
    restored.UnitIds[1].Should().Be(11);
    restored.UnitIds[2].Should().Be(12);
  }

  [Fact]
  public void SerializedSize_IncludesSourceUnitIds() {
    var command = new AttackCommand { TargetUnitId = 44 };
    command.GetSerializedSize().Should().Be(18);

    command.UnitIds.Add(10);
    command.UnitIds.Add(11);

    command.GetSerializedSize().Should().Be(26);
  }

  private static AttackCommand RoundTrip(AttackCommand original) {
    var buffer = new byte[original.GetSerializedSize()];
    var writer = new SpanWriter(buffer);
    original.Serialize(ref writer);

    var restored = new AttackCommand();
    var reader = new SpanReader(buffer);
    restored.Deserialize(ref reader);
    return restored;
  }
}
