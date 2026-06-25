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
      command.AddSourceUnitId(i + 1);

    command.SourceUnitIdCount.Should().Be(12);
    for (int i = 0; i < command.SourceUnitIdCount; i++)
      command.GetSourceUnitId(i).Should().Be(i + 1);
  }

  [Fact]
  public void Serialize_RoundTripsTargetAndSourceUnitIds() {
    var original = new AttackCommand {
      PlayerId = 2,
      Tick = 17,
      TargetUnitId = 44,
    };
    original.AddSourceUnitId(10);
    original.AddSourceUnitId(11);
    original.AddSourceUnitId(12);

    var restored = RoundTrip(original);

    restored.PlayerId.Should().Be(2);
    restored.Tick.Should().Be(17);
    restored.TargetUnitId.Should().Be(44);
    restored.SourceUnitIdCount.Should().Be(3);
    restored.GetSourceUnitId(0).Should().Be(10);
    restored.GetSourceUnitId(1).Should().Be(11);
    restored.GetSourceUnitId(2).Should().Be(12);
  }

  [Fact]
  public void SerializedSize_IncludesSourceUnitIds() {
    var command = new AttackCommand { TargetUnitId = 44 };
    command.GetSerializedSize().Should().Be(18);

    command.AddSourceUnitId(10);
    command.AddSourceUnitId(11);

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
