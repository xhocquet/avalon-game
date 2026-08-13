using System.Collections.Generic;
using FluentAssertions;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Navigation;
using Xunit;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Tests;

public class GroupFormationTests {
  // CommandSystem never calls Solve with an empty list, but the centroid divide inside GetForward
  // shouldn't depend on that: an empty group must fall back to +Z, not divide by zero.
  [Fact]
  public void Solve_WithNoUnitsProducesNoDestinations() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    var rules = frame.AssetRegistry.Get<MovementRulesAsset>();
    var units = new List<FormationUnit>();
    var destinations = new List<FPVector3> { FPVector3.Zero };

    GroupFormation.Solve(units, new FPVector3(FP64.FromInt(5), FP64.Zero, FP64.FromInt(5)), rules,
        navMesh: null, query: null, destinations);

    destinations.Should().BeEmpty();
  }

  [Fact]
  public void Solve_PlacesHeroesOnTargetAndMinionsBehindThem() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    var rules = frame.AssetRegistry.Get<MovementRulesAsset>();
    var target = new FPVector3(FP64.FromInt(10), FP64.Zero, FP64.Zero);
    var units = new List<FormationUnit> {
      new(default, unitId: 2, isHero: false, FPVector3.Zero),
      new(default, unitId: 1, isHero: true, FPVector3.Zero),
    };
    var destinations = new List<FPVector3>();

    GroupFormation.Solve(units, target, rules, navMesh: null, query: null, destinations);

    destinations.Count.Should().Be(2);
    // Sorted heroes-first, so index 0 is the hero: single hero sits on the click itself.
    units[0].IsHero.Should().BeTrue();
    destinations[0].x.Should().Be(target.x);
    // Group travels +X, so the minion blob backs off along -X behind the hero.
    destinations[1].x.Should().BeLessThan(target.x);
  }
}
