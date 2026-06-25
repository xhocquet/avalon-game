using FluentAssertions;
using Xunit;

namespace Meesles.Avalon.Sim.Tests;

public class NavigationRuntimeTests {
  [Fact]
  public void SimHarness_LoadsBakedNavigationRuntime() {
    var harness = SimHarness.CreateInitialized();

    harness.Navigation.NavMesh.Vertices.Length.Should().BeGreaterThan(0);
    harness.Navigation.NavMesh.Triangles.Length.Should().BeGreaterThan(0);
    harness.Navigation.Query.Should().NotBeNull();
    harness.Navigation.Pathfinder.Should().NotBeNull();
    harness.Navigation.Funnel.Should().NotBeNull();
    harness.Navigation.Avoidance.Should().NotBeNull();
    harness.Navigation.AgentSystem.Should().NotBeNull();
  }
}
