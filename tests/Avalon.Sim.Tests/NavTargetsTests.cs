using System.Collections.Generic;
using FluentAssertions;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using Meesles.Avalon.Sim.Navigation;
using Xunit;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Tests;

// Right-clicking a rock, a tree or a structure lands on a hole in the navmesh, and the nearest
// walkable point sits exactly ON the hole's boundary. A destination on the boundary is one the agent
// grinds along instead of arriving at, so NavTargets backs it off by MoveTargetEdgeClearance.
public class NavTargetsTests {
  [Fact]
  public void ResolveMoveTarget_LeavesAClickInOpenGroundAlone() {
    var harness = SimHarness.CreateInitialized();
    var openGround = OpenGroundSpot(harness);

    Resolve(harness, openGround).Should().Be(openGround);
  }

  // A hero spawns close enough to its base wall that the clearance push moves the point; it must
  // still land near where the player clicked rather than somewhere across the map.
  [Fact]
  public void ResolveMoveTarget_KeepsAWalkableClickNearWhereItWasClicked() {
    var harness = SimHarness.CreateInitialized();
    var clicked = harness.Frame.GetReadOnly<TransformComponent>(harness.FindHero(1)).Position;

    var resolved = Resolve(harness, clicked);

    TriangleAt(harness, resolved).Should().BeGreaterOrEqualTo(0);
    FPVector2.Distance(resolved.ToXZ(), clicked.ToXZ())
      .Should().BeLessOrEqualTo(Clearance(harness) * FP64.FromInt(2));
  }

  [Fact]
  public void ResolveMoveTarget_BacksOffTheBoundaryByTheAuthoredClearance() {
    var harness = SimHarness.CreateInitialized();
    var structurePosition = HostileTurretPosition(harness);
    TriangleAt(harness, structurePosition).Should().BeLessThan(0,
      "this test is only meaningful while structures actually hole the navmesh");

    var onTheEdge = NavTargets.SnapToWalkable(harness.Navigation.Query, structurePosition);
    var resolved = Resolve(harness, structurePosition);

    TriangleAt(harness, resolved).Should().BeGreaterOrEqualTo(0, "the destination has to be walkable");
    FPVector2.Distance(resolved.ToXZ(), onTheEdge.ToXZ())
      .Should().BeGreaterOrEqualTo(Clearance(harness) - Epsilon,
        "the whole point is breathing room between the destination and the hole it snapped off");
  }

  // The client resolves the click and sends the result; CommandSystem resolves that result again.
  // The two must agree, or the marker draws somewhere the unit was never sent.
  [Fact]
  public void ResolveMoveTarget_IsIdempotent() {
    var harness = SimHarness.CreateInitialized();
    var resolved = Resolve(harness, HostileTurretPosition(harness));

    Resolve(harness, resolved).Should().Be(resolved);
  }

  [Fact]
  public void ResolveMoveTarget_PullsAClickPastTheMapEdgeBackOntoTheMesh() {
    var harness = SimHarness.CreateInitialized();
    var offMap = new FPVector3(FP64.FromInt(10000), FP64.Zero, FP64.FromInt(10000));

    TriangleAt(harness, Resolve(harness, offMap)).Should().BeGreaterOrEqualTo(0);
  }

  private static readonly FP64 Epsilon = FP64.FromDouble(0.01);

  private static FPVector3 Resolve(SimHarness harness, FPVector3 target) {
    return NavTargets.ResolveMoveTarget(harness.Navigation.NavMesh, harness.Navigation.Query, target,
      Clearance(harness));
  }

  private static FP64 Clearance(SimHarness harness) {
    return harness.AssetRegistry.Get<MovementRulesAsset>().MoveTargetEdgeClearance;
  }

  private static int TriangleAt(SimHarness harness, FPVector3 position) {
    return harness.Navigation.Query.FindTriangle(position.ToXZ());
  }

  // Centroid of the largest navmesh triangle: the one spot the map can promise is open ground
  // rather than a corner or a corridor, so nothing about it needs pushing.
  private static FPVector3 OpenGroundSpot(SimHarness harness) {
    var navMesh = harness.Navigation.NavMesh;
    var best = 0;
    for (var i = 1; i < navMesh.Triangles.Length; i++)
      if (navMesh.Triangles[i].area > navMesh.Triangles[best].area)
        best = i;

    var centroid = navMesh.Triangles[best].centerXZ;
    return new FPVector3(centroid.x, FP64.Zero, centroid.y);
  }

  private static FPVector3 HostileTurretPosition(SimHarness harness) {
    var frame = harness.Frame;
    var heroTeamId = frame.GetReadOnly<TeamComponent>(harness.FindHero(1)).TeamId;

    var candidates = new List<(int UnitId, FPVector3 Position)>();
    var filter = frame.Filter<Turret, UnitIdComponent, TeamComponent, TransformComponent>();
    while (filter.Next(out var entity)) {
      if (frame.GetReadOnly<TeamComponent>(entity).TeamId == heroTeamId)
        continue;

      candidates.Add((frame.GetReadOnly<UnitIdComponent>(entity).UnitId,
        frame.GetReadOnly<TransformComponent>(entity).Position));
    }

    candidates.Should().NotBeEmpty();
    candidates.Sort((a, b) => a.UnitId.CompareTo(b.UnitId));
    return candidates[0].Position;
  }
}
