using System;
using FluentAssertions;
using Meesles.Avalon;
using Meesles.Avalon.Sim.Commands;
using Meesles.Avalon.Sim.Components;
using Xunit;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim.Tests;

// Commands arrive from untrusted peers, and nothing between the socket and the simulation catches an
// exception: LiteNetLib's ProcessEvent, ServerNetworkService.HandleClientInputMessage and
// ServerLoop.ExecuteCycle all let one propagate, and ServerLoop.Run has no catch. A payload that
// throws or corrupts state during deserialization takes the process down with every room on it.
public class CommandValidationTests {
  private const int FactionA = 200;
  private const int MoveCommandTypeId = 100;

  // A 30-byte MoveCommand frame carries no unit ids. Declaring 32767 of them used to size an array
  // from the wire count and then run the reader off the end of the span.
  [Fact]
  public void Deserialize_CountExceedingPayload_IsRejectedWithoutThrowing() {
    var command = new MoveCommand();

    var act = () => DeserializeMoveFrame(command, declaredUnitIds: short.MaxValue, trailingIds: 0);

    act.Should().NotThrow();
    command.UnitIds.IsValid.Should().BeFalse();
    command.UnitIds.Count.Should().Be(0);
  }

  [Fact]
  public void Deserialize_NegativeCount_IsRejectedWithoutLeavingNegativeCount() {
    var command = new MoveCommand();

    DeserializeMoveFrame(command, declaredUnitIds: -5, trailingIds: 0);

    command.UnitIds.IsValid.Should().BeFalse();
    command.UnitIds.Count.Should().Be(0, "a negative Count corrupts every loop that reads the list");
  }

  // Over-cap but with the bytes actually present: catchup and spectator batches read several commands
  // from one reader, so the payload has to be consumed even though the order is dropped.
  [Fact]
  public void Deserialize_CountOverCap_ConsumesPayloadAndRejects() {
    var overCap = CommandLimits.MaxSelectedUnits + 1;
    var command = new MoveCommand();

    var consumed = DeserializeMoveFrame(command, declaredUnitIds: (short)overCap, trailingIds: overCap);

    command.UnitIds.IsValid.Should().BeFalse();
    command.UnitIds.Count.Should().Be(0);
    consumed.Should().Be(30 + overCap * 4, "the whole declared payload must be read past");
  }

  [Fact]
  public void Deserialize_CountAtCap_IsAccepted() {
    var command = new MoveCommand();

    DeserializeMoveFrame(command, declaredUnitIds: CommandLimits.MaxSelectedUnits,
      trailingIds: CommandLimits.MaxSelectedUnits);

    command.UnitIds.IsValid.Should().BeTrue();
    command.UnitIds.Count.Should().Be(CommandLimits.MaxSelectedUnits);
    command.UnitIds[0].Should().Be(1);
    command.UnitIds[CommandLimits.MaxSelectedUnits - 1].Should().Be(CommandLimits.MaxSelectedUnits);
  }

  // The write side is the other half: a count past short.MaxValue truncated to a negative Int16 while
  // the body kept its full length, so the receiver could never recover the selection.
  [Fact]
  public void Add_StopsAtCap_SoTheInt16CountCannotTruncate() {
    var command = new MoveCommand();

    for (var i = 0; i < CommandLimits.MaxSelectedUnits + 50; i++)
      command.UnitIds.Add(i + 1);

    command.UnitIds.Count.Should().Be(CommandLimits.MaxSelectedUnits);
    ((short)command.UnitIds.Count).Should().Be(CommandLimits.MaxSelectedUnits);
    command.UnitIds.Add(999).Should().BeFalse("the caller needs to know the order was clamped");
  }

  [Fact]
  public void Add_RoundTripsAtCap() {
    var original = new MoveCommand { PlayerId = 3, Tick = 7 };
    for (var i = 0; i < CommandLimits.MaxSelectedUnits; i++)
      original.UnitIds.Add(i + 1);

    var buffer = new byte[original.GetSerializedSize()];
    var writer = new SpanWriter(buffer);
    original.Serialize(ref writer);
    writer.Position.Should().Be(original.GetSerializedSize());

    var restored = new MoveCommand();
    var reader = new SpanReader(buffer);
    restored.Deserialize(ref reader);

    restored.UnitIds.IsValid.Should().BeTrue();
    restored.UnitIds.Count.Should().Be(CommandLimits.MaxSelectedUnits);
    restored.UnitIds[CommandLimits.MaxSelectedUnits - 1].Should().Be(CommandLimits.MaxSelectedUnits);
  }

  // A pooled command is reused across payloads with only PlayerId and Tick reset, so the rejection
  // flag has to be reassigned on every pass or a rejected frame poisons the next tenant.
  [Fact]
  public void Deserialize_ClearsRejectionFromPreviousUseOfAPooledInstance() {
    var command = new MoveCommand();
    DeserializeMoveFrame(command, declaredUnitIds: -1, trailingIds: 0);
    command.UnitIds.IsValid.Should().BeFalse();

    DeserializeMoveFrame(command, declaredUnitIds: 2, trailingIds: 2);

    command.UnitIds.IsValid.Should().BeTrue();
    command.UnitIds.Count.Should().Be(2);
  }

  [Fact]
  public void MoveCommand_OutOfEnvelopeTarget_IsNotExecuted() {
    var harness = SimHarness.CreateInitialized();
    var far = FP64.FromInt(CommandLimits.MaxWorldCoordinate + 1);

    harness.Tick(SimHarness.MoveCommand(playerId: 1, tick: 0, targetX: far, targetZ: FP64.Zero));

    harness.Count<UnitMoveTarget>().Should().Be(0, "an out-of-envelope target must not reach a handler");
  }

  [Fact]
  public void MoveCommand_InEnvelopeTarget_IsExecuted() {
    var harness = SimHarness.CreateInitialized();

    harness.Tick(SimHarness.MoveCommand(playerId: 1, tick: 0,
      targetX: FP64.FromInt(10), targetZ: FP64.FromInt(10)));

    harness.Count<UnitMoveTarget>().Should().BeGreaterThan(0);
  }

  [Fact]
  public void MoveCommand_MalformedSelection_DropsTheWholeOrder() {
    var harness = SimHarness.CreateInitialized();
    var command = SimHarness.MoveCommand(playerId: 1, tick: 0,
      targetX: FP64.FromInt(10), targetZ: FP64.FromInt(10));
    DeserializeMoveFrame(command, declaredUnitIds: -1, trailingIds: 0);
    command.PlayerId = 1;
    command.TargetX = FP64.FromInt(10);
    command.TargetZ = FP64.FromInt(10);

    harness.Tick(command);

    harness.Count<UnitMoveTarget>().Should().Be(0,
      "a corrupt selection must drop the order, not fall through to the hero-only move path");
  }

  // FactionCatalog.Resolve throws on an unregistered id, and the pick replicates through the
  // authoritative sim to every client, so an unvalidated id crashes the other players' view layer.
  [Fact]
  public void SelectFactionCommand_UnknownFactionId_IsNotApplied() {
    var harness = SimHarness.CreateInitialized(spawnHeroesNow: false);

    harness.Tick(SimHarness.SelectFactionCommand(playerId: 1, tick: 0, factionId: 999999));

    harness.Count<Hero>().Should().Be(0, "an unknown faction id must not confirm a slot");
    SlotFactionId(harness, playerId: 1).Should().Be(SimulationSetup.DefaultFactionId);
  }

  [Fact]
  public void SelectFactionCommand_KnownFactionId_IsApplied() {
    var harness = SimHarness.CreateInitialized(spawnHeroesNow: false);

    harness.Tick(SimHarness.SelectFactionCommand(playerId: 1, tick: 0, factionId: FactionA));

    harness.Count<Hero>().Should().Be(1);
    SlotFactionId(harness, playerId: 1).Should().Be(FactionA);
  }

  [Fact]
  public void SelectFactionCommand_AfterHeroSpawned_IsIgnored() {
    var harness = SimHarness.CreateInitialized(spawnHeroesNow: false);
    harness.Tick(SimHarness.SelectFactionCommand(playerId: 1, tick: 0, factionId: FactionA));
    harness.Count<Hero>().Should().Be(1);

    harness.Tick(SimHarness.SelectFactionCommand(playerId: 1, tick: 1, factionId: 201));

    SlotFactionId(harness, playerId: 1).Should().Be(FactionA, "the pick is settled once the hero exists");
  }

  // Writes a MoveCommand frame by hand so the unit-id count can disagree with the bytes that follow,
  // then deserializes it and reports how far the reader advanced.
  private static int DeserializeMoveFrame(MoveCommand command, short declaredUnitIds, int trailingIds) {
    var buffer = new byte[30 + trailingIds * 4];
    var writer = new SpanWriter(buffer);
    writer.WriteInt32(MoveCommandTypeId);
    writer.WriteInt32(0);
    writer.WriteInt32(0);
    writer.WriteFP(FP64.Zero);
    writer.WriteFP(FP64.Zero);
    writer.WriteInt16(declaredUnitIds);
    for (var i = 0; i < trailingIds; i++)
      writer.WriteInt32(i + 1);

    var reader = new SpanReader(buffer);
    command.Deserialize(ref reader);
    return reader.Position;
  }

  private static int SlotFactionId(SimHarness harness, int playerId) {
    var frame = harness.Frame;
    var filter = frame.Filter<PlayerFaction>();
    while (filter.Next(out var entity)) {
      ref readonly var slot = ref frame.GetReadOnly<PlayerFaction>(entity);
      if (slot.PlayerId == playerId)
        return slot.FactionId;
    }

    throw new InvalidOperationException($"No faction slot for player {playerId}.");
  }
}
