using System.Collections.Generic;
using FluentAssertions;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using Xunit;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.Deterministic.Navigation;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Tests;

// A turret or crystal stands in the hole its own footprint carves out of the navmesh, so an attack
// order aimed at its centre used to hand the agent a destination FindPath could not resolve: the
// pathfinder logged "end=... is outside NavMesh" and the hero never moved.
public class StructureAttackApproachTests {
  private const int HeroPlayerId = 1;
  private const int HeroUnitId = 3;

  [Fact]
  public void AttackOrderOnTurret_LandsOnTheNavMesh() {
    var harness = SimHarness.CreateInitialized();
    var (turretUnitId, turretPosition) = FindHostileTurret(harness);

    TriangleAt(harness, turretPosition).Should().BeLessThan(0,
      "this test is only meaningful while structures actually hole the navmesh");

    harness.Tick(SimHarness.AttackCommand(HeroPlayerId, 0, turretUnitId, HeroUnitId));

    var moveTarget = GetMoveTarget(harness, HeroUnitId);
    TriangleAt(harness, moveTarget).Should().BeGreaterOrEqualTo(0, "the order must be reachable");
    FPVector2.Distance(moveTarget.ToXZ(), turretPosition.ToXZ())
      .Should().BeLessThan(GetAttackRange(harness, HeroUnitId),
        "the approach point has to sit inside attack range or the hero can never engage");
  }

  [Fact]
  public void AttackOrderOnTurret_MovesTheHero() {
    var harness = SimHarness.CreateInitialized();
    var (turretUnitId, turretPosition) = FindHostileTurret(harness);
    harness.Tick();

    harness.Tick(SimHarness.AttackCommand(HeroPlayerId, 1, turretUnitId, HeroUnitId));
    harness.Tick();

    var nav = HeroNav(harness);
    nav.Status.Should().NotBe((byte)FPNavAgentStatus.PathFailed);
    nav.HasPath.Should().BeTrue();

    var before = GetPosition(harness, HeroUnitId);
    for (int i = 0; i < 10; i++)
      harness.Tick();

    var after = GetPosition(harness, HeroUnitId);
    FPVector2.Distance(after.ToXZ(), turretPosition.ToXZ())
      .Should().BeLessThan(FPVector2.Distance(before.ToXZ(), turretPosition.ToXZ()),
        "the hero should be closing on the turret");
  }

  // The chase is re-aimed every tick by AttackIntentSystem, so the snap has to hold there too, not
  // just on the tick the order lands.
  [Fact]
  public void AttackOrderOnTurret_ClosesToAttackRangeAndDamagesIt() {
    var harness = SimHarness.CreateInitialized();
    var (turretUnitId, turretPosition) = FindHostileTurret(harness);

    // Parked outside attack range but close enough that the walk is a handful of ticks.
    SetPosition(harness, HeroUnitId, ApproachFrom(harness, turretPosition, FP64.FromInt(10)));
    var startHealth = GetHealth(harness, turretUnitId);

    harness.Tick(SimHarness.AttackCommand(HeroPlayerId, 0, turretUnitId, HeroUnitId));
    for (int i = 0; i < 120; i++)
      harness.Tick();

    GetHealth(harness, turretUnitId).Should().BeLessThan(startHealth);
  }

  // The navmesh hole a turret carves is ~1.6m across, wider than any melee hero's authored reach,
  // so a centre-to-centre range check leaves them chasing a structure they can never touch. Every
  // faction has to be able to engage, not just the ranged default the other tests spawn.
  [Theory]
  [InlineData(AssetIds.FactionHairyWizards)]
  [InlineData(AssetIds.FactionSnailheads)]
  [InlineData(AssetIds.FactionCrystalWarriors)]
  [InlineData(AssetIds.FactionSkinwalkerTribe)]
  [InlineData(AssetIds.FactionPickleKnights)]
  public void EveryFactionsHero_EngagesATurretItWalksTo(int factionId) {
    var harness = SimHarness.CreateInitialized(spawnHeroesNow: false);
    harness.Tick(SimHarness.SelectFactionCommand(HeroPlayerId, 0, factionId));

    var heroUnitId = harness.Frame.GetReadOnly<UnitIdentity>(harness.FindHero(HeroPlayerId)).UnitId;
    var (turretUnitId, turretPosition) = FindHostileTurret(harness);

    SetPosition(harness, heroUnitId, ApproachFrom(harness, turretPosition, FP64.FromInt(6)));
    var startHealth = GetHealth(harness, turretUnitId);

    harness.Tick(SimHarness.AttackCommand(HeroPlayerId, 1, turretUnitId, heroUnitId));
    for (int i = 0; i < 200; i++)
      harness.Tick();

    harness.Frame.GetReadOnly<Combat>(Entity(harness, heroUnitId)).TargetUnitId
      .Should().Be(turretUnitId, "the hero has to close to a range it can actually reach from");
    GetHealth(harness, turretUnitId).Should().BeLessThan(startHealth);
  }

  // A walkable spot `distance` away from the structure, on the map-centre side so the walk stays
  // inside the mesh regardless of which corner the turret sits in.
  private static FPVector3 ApproachFrom(SimHarness harness, FPVector3 structurePosition, FP64 distance) {
    var inward = (FPVector3.Zero - structurePosition).ToXZ().normalized * distance;
    var spot = new FPVector3(structurePosition.x + inward.x, FP64.Zero, structurePosition.z + inward.y);
    return Navigation.NavTargets.SnapToWalkable(harness.Navigation.Query, spot);
  }

  private static int TriangleAt(SimHarness harness, FPVector3 position) {
    return harness.Navigation.Query.FindTriangle(position.ToXZ());
  }

  private static (int UnitId, FPVector3 Position) FindHostileTurret(SimHarness harness) {
    var frame = harness.Frame;
    int heroTeamId = frame.GetReadOnly<Team>(harness.FindHero(HeroPlayerId)).TeamId;

    var candidates = new List<(int UnitId, FPVector3 Position)>();
    var filter = frame.Filter<Turret, UnitIdentity, Team, TransformComponent>();
    while (filter.Next(out var entity)) {
      if (frame.GetReadOnly<Team>(entity).TeamId == heroTeamId)
        continue;

      candidates.Add((frame.GetReadOnly<UnitIdentity>(entity).UnitId,
        frame.GetReadOnly<TransformComponent>(entity).Position));
    }

    candidates.Should().NotBeEmpty();
    candidates.Sort((a, b) => a.UnitId.CompareTo(b.UnitId));
    return candidates[0];
  }

  private static NavAgentComponent HeroNav(SimHarness harness) {
    return harness.Frame.GetReadOnly<NavAgentComponent>(harness.FindHero(HeroPlayerId));
  }

  private static FPVector3 GetMoveTarget(SimHarness harness, int unitId) {
    var frame = harness.Frame;
    var entity = Entity(harness, unitId);
    frame.Has<UnitMoveTarget>(entity).Should().BeTrue();
    return frame.GetReadOnly<UnitMoveTarget>(entity).Target;
  }

  private static FPVector3 GetPosition(SimHarness harness, int unitId) {
    return harness.Frame.GetReadOnly<TransformComponent>(Entity(harness, unitId)).Position;
  }

  private static FP64 GetAttackRange(SimHarness harness, int unitId) {
    return harness.Frame.GetReadOnly<Stats>(Entity(harness, unitId)).AttackRange;
  }

  private static FP64 GetHealth(SimHarness harness, int unitId) {
    return harness.Frame.GetReadOnly<Health>(Entity(harness, unitId)).Current;
  }

  private static void SetPosition(SimHarness harness, int unitId, FPVector3 position) {
    var frame = harness.Frame;
    ref var transform = ref frame.Get<TransformComponent>(Entity(harness, unitId));
    transform.Position = position;
  }

  private static EntityRef Entity(SimHarness harness, int unitId) {
    var frame = harness.Frame;
    UnitLookup.TryGetEntityByUnitId(ref frame, unitId, out var entity).Should().BeTrue();
    return entity;
  }
}
