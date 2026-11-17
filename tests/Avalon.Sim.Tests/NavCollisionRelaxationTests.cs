using FluentAssertions;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.Deterministic.Navigation;
using xpTURN.Klotho.ECS;
using Xunit;

namespace Meesles.Avalon.Sim.Tests;

public class NavCollisionRelaxationTests {
  [Fact]
  public void StationaryOverlap_SettlesAcrossTicksWithoutChangingVelocity() {
    var harness = SimHarness.CreateInitialized();
    harness.Navigation.AgentSystem.Tuning.CollisionResolveIterations.Should().Be(1);
    var frame = harness.Frame;
    var agents = AddPair(ref frame, harness);
    var previous = Penetration(ref frame, agents);
    for (var tick = 0; tick < 8; tick++) {
      harness.Navigation.AgentSystem.UpdateMovement(ref frame, agents, agents.Length, tick, FP64.Zero);
      var penetration = Penetration(ref frame, agents);
      penetration.Should().BeLessThan(previous);
      previous = penetration;
      foreach (var entity in agents) {
        var nav = frame.GetReadOnly<NavAgentComponent>(entity);
        nav.Velocity.Should().Be(FPVector2.Zero);
        nav.CurrentTriangleIndex.Should().BeGreaterThanOrEqualTo(0);
        harness.Navigation.Query.FindTriangle(nav.Position.ToXZ()).Should().BeGreaterThanOrEqualTo(0);
      }
    }
    previous.Should().BeLessThan(FP64.FromDouble(.001));
  }

  [Fact]
  public void FourSinglePassTicks_MatchOneFourPassCorrection() {
    var single = SimHarness.CreateInitialized(collisionResolveIterations: 1);
    var four = SimHarness.CreateInitialized(collisionResolveIterations: 4);
    var singleFrame = single.Frame;
    var fourFrame = four.Frame;
    var singleAgents = AddPair(ref singleFrame, single);
    var fourAgents = AddPair(ref fourFrame, four);
    for (var tick = 0; tick < 4; tick++)
      single.Navigation.AgentSystem.UpdateMovement(ref singleFrame, singleAgents, 2, tick, FP64.Zero);
    four.Navigation.AgentSystem.UpdateMovement(ref fourFrame, fourAgents, 2, 0, FP64.Zero);
    for (var i = 0; i < 2; i++)
      singleFrame.GetReadOnly<NavAgentComponent>(singleAgents[i]).Position.Should()
        .Be(fourFrame.GetReadOnly<NavAgentComponent>(fourAgents[i]).Position);
  }

  private static EntityRef[] AddPair(ref Frame frame, SimHarness harness) {
    var entities = new EntityRef[2];
    var origin = FindOpenPosition(harness);
    for (var i = 0; i < 2; i++) {
      var position = origin + new FPVector3(FP64.FromDouble(i * .5), FP64.Zero, FP64.Zero);
      var agent = new NavAgentComponent();
      NavAgentComponent.Init(ref agent, position);
      agent.Radius = FP64.Half;
      agent.Status = (byte)FPNavAgentStatus.Arrived;
      agent.CurrentTriangleIndex = harness.Navigation.Query.FindTriangle(position.ToXZ());
      agent.CurrentTriangleIndex.Should().BeGreaterThanOrEqualTo(0);
      entities[i] = frame.CreateEntity();
      frame.Add(entities[i], agent);
    }
    return entities;
  }

  private static FPVector3 FindOpenPosition(SimHarness harness) {
    var query = harness.Navigation.Query;
    for (var x = -40; x <= 40; x += 4)
      for (var z = -40; z <= 40; z += 4) {
        var position = new FPVector3(FP64.FromInt(x), FP64.Zero, FP64.FromInt(z));
        var triangle = query.FindTriangle(position.ToXZ());
        if (triangle < 0) continue;
        var left = position - new FPVector3(FP64.One, FP64.Zero, FP64.Zero);
        var right = position + new FPVector3(FP64.One, FP64.Zero, FP64.Zero);
        var agent = new NavAgentComponent();
        NavAgentComponent.Init(ref agent, position);
        var mask = FPNavAgentSystem.ResolveWalkMask(agent);
        if (query.MoveAlongSurface(position, left, triangle, mask, FP64.One).resultPos == left &&
            query.MoveAlongSurface(position, right, triangle, mask, FP64.One).resultPos == right)
          return position;
      }
    throw new System.InvalidOperationException("No open collision test position found.");
  }

  private static FP64 Penetration(ref Frame frame, EntityRef[] entities) {
    var first = frame.GetReadOnly<NavAgentComponent>(entities[0]);
    var second = frame.GetReadOnly<NavAgentComponent>(entities[1]);
    return first.Radius + second.Radius - (first.Position - second.Position).magnitude;
  }
}
