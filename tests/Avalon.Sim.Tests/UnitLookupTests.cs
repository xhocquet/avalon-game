using FluentAssertions;
using Meesles.Avalon.Sim.Components;
using Xunit;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Tests;

public class UnitLookupTests {
  [Fact]
  public void TryGetEntityByUnitId_ResolvesExistingUnit() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;

    bool found = UnitLookup.TryGetEntityByUnitId(ref frame, unitId: 1, out var entity);

    found.Should().BeTrue();
    entity.IsValid.Should().BeTrue();
    frame.GetReadOnly<UnitIdComponent>(entity).UnitId.Should().Be(1);
  }

  [Fact]
  public void TryGetEntityByUnitId_ReturnsFalseForMissingUnit() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;

    bool found = UnitLookup.TryGetEntityByUnitId(ref frame, unitId: 999, out var entity);

    found.Should().BeFalse();
    entity.IsValid.Should().BeFalse();
  }

  [Fact]
  public void TryGetPlayerOwnedUnitById_ResolvesOwnTeamUnit() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    int playerOneUnitId = GetPlayerUnitId(harness, playerId: 1);

    bool found = UnitLookup.TryGetPlayerOwnedUnitById(ref frame, playerId: 1, playerOneUnitId, out var entity);

    found.Should().BeTrue();
    entity.IsValid.Should().BeTrue();
    frame.GetReadOnly<UnitIdComponent>(entity).UnitId.Should().Be(playerOneUnitId);
    frame.GetReadOnly<TeamComponent>(entity).TeamId.Should().Be(1);
  }

  [Fact]
  public void TryGetPlayerOwnedUnitById_RejectsEnemyTeamUnit() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    int playerTwoUnitId = GetPlayerUnitId(harness, playerId: 2);

    bool found = UnitLookup.TryGetPlayerOwnedUnitById(ref frame, playerId: 1, playerTwoUnitId, out var entity);

    found.Should().BeFalse();
    entity.IsValid.Should().BeFalse();
  }

  [Fact]
  public void TryGetPlayerControllableUnitById_RejectsOwnTeamTurret() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    int turretUnitId = GetTurretUnitId(harness, teamId: 1);

    bool found = UnitLookup.TryGetPlayerControllableUnitById(ref frame, playerId: 1, turretUnitId, out var entity);

    found.Should().BeFalse();
    entity.IsValid.Should().BeFalse();
  }

  [Fact]
  public void TryGetEntityByUnitId_ReturnsFalseAfterEntityDestroyed() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    int playerOneUnitId = GetPlayerUnitId(harness, playerId: 1);
    UnitLookup.TryGetEntityByUnitId(ref frame, playerOneUnitId, out var entity).Should().BeTrue();

    frame.DestroyEntity(entity);

    UnitLookup.TryGetEntityByUnitId(ref frame, playerOneUnitId, out var destroyedEntity).Should().BeFalse();
    destroyedEntity.IsValid.Should().BeFalse();
  }

  private static int GetPlayerUnitId(SimHarness harness, int playerId) {
    var frame = harness.Frame;
    var filter = frame.Filter<Player, UnitIdComponent>();
    while (filter.Next(out var entity)) {
      ref readonly var player = ref frame.GetReadOnly<Player>(entity);
      if (player.PlayerId != playerId)
        continue;

      return frame.GetReadOnly<UnitIdComponent>(entity).UnitId;
    }

    throw new Xunit.Sdk.XunitException($"Player {playerId} unit was not found.");
  }

  private static int GetTurretUnitId(SimHarness harness, int teamId) {
    var frame = harness.Frame;
    var filter = frame.Filter<Turret, TeamComponent, UnitIdComponent>();
    while (filter.Next(out var entity)) {
      ref readonly var team = ref frame.GetReadOnly<TeamComponent>(entity);
      if (team.TeamId != teamId)
        continue;

      return frame.GetReadOnly<UnitIdComponent>(entity).UnitId;
    }

    throw new Xunit.Sdk.XunitException($"Team {teamId} turret unit was not found.");
  }
}
