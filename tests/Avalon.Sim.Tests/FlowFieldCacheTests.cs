using FluentAssertions;
using Meesles.Avalon.Sim.Navigation;
using Xunit;

namespace Meesles.Avalon.Sim.Tests;

public class FlowFieldCacheTests {
  [Fact]
  public void Cache_StaysBounded_AcrossEveryGoalTriangle() {
    var harness = SimHarness.CreateInitialized();
    var cache = harness.Navigation.FlowFields;

    for (var goal = 0; goal < harness.Navigation.NavMesh.Triangles.Length; goal++)
      cache.GetOrCreate(goal);

    cache.Count.Should().BeLessThan(harness.Navigation.NavMesh.Triangles.Length);
  }

  // Eviction must be invisible to the simulation: a rebuilt field routes exactly like the one it
  // replaced, whichever ticks it was built on.
  [Fact]
  public void Rebuilt_Field_MatchesTheEvictedOne() {
    var harness = SimHarness.CreateInitialized();
    var cache = harness.Navigation.FlowFields;
    var triangleCount = harness.Navigation.NavMesh.Triangles.Length;

    var goal = triangleCount / 2;
    var original = cache.GetOrCreate(goal);
    var originalRoute = (int[])original.NextTriangle.Clone();
    var originalExit = original.GetExitDirection(0);

    for (var other = 0; other < triangleCount; other++)
      cache.GetOrCreate(other);

    var rebuilt = cache.GetOrCreate(goal);
    rebuilt.Should().NotBeSameAs(original);
    rebuilt.NextTriangle.Should().Equal(originalRoute);
    rebuilt.GetExitDirection(0).Should().Be(originalExit);
  }

  [Fact]
  public void Invalidate_DropsEverything() {
    var harness = SimHarness.CreateInitialized();
    var cache = harness.Navigation.FlowFields;

    cache.GetOrCreate(0);
    var version = cache.Version;

    cache.Invalidate();

    cache.Count.Should().Be(0);
    cache.Version.Should().Be(version + 1);
  }
}
