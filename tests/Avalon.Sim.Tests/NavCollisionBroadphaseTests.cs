using System;
using FluentAssertions;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.Deterministic.Navigation;
using xpTURN.Klotho.ECS;
using Xunit;

namespace Meesles.Avalon.Sim.Tests;

public class NavCollisionBroadphaseTests {
  [Theory]
  [InlineData(0, 0, 1)]
  [InlineData(1, 1, 1)]
  [InlineData(2, 2, 1)]
  [InlineData(32, 3, 1)]
  [InlineData(64, 4, 1)]
  [InlineData(72, 5, 1)]
  [InlineData(64, 6, 1)]
  [InlineData(64, 7, 1)]
  [InlineData(64, 8, 1)]
  [InlineData(64, 4, 4)]
  [InlineData(64, 6, 4)]
  [InlineData(64, 7, 4)]
  public void Correction_MatchesAllPairsBitForBit(int count, int seed, int passes) {
    var actual = SimHarness.CreateInitialized(collisionResolveIterations: passes);
    var expected = SimHarness.CreateInitialized(collisionResolveIterations: passes);
    actual.Navigation.AgentSystem.Tuning.CollisionResolveIterations.Should().Be(passes);
    var actualFrame = actual.Frame;
    var expectedFrame = expected.Frame;
    var random = new Random(seed);
    var agents = new EntityRef[count];
    var referenceAgents = new EntityRef[count];
    for (var i = 0; i < count; i++) {
      var x = seed == 6 ? random.Next(-40, 41) : random.Next(-4, 5);
      var z = seed == 6 ? random.Next(-40, 41) : random.Next(-4, 5);
      var position = new FPVector3(FP64.FromInt(x) + FP64.FromRaw(i % 3 - 1), FP64.Zero,
        FP64.FromInt(z) + FP64.FromRaw(i % 3 - 1));
      if (seed == 7) position = FPVector3.Zero; // Coincident crowd.
      var nav = new NavAgentComponent();
      NavAgentComponent.Init(ref nav, position);
      nav.CurrentTriangleIndex = actual.Navigation.Query.FindTriangle(position.ToXZ());
      nav.Radius = FP64.FromDouble(new[] { 0.0, 0.25, 0.5, 1.0, 2.5 }[i % 5]);
      if (seed == 8) nav.Radius = FP64.FromRaw(1);
      nav.Status = (byte)FPNavAgentStatus.Arrived;
      agents[i] = actualFrame.CreateEntity();
      actualFrame.Add(agents[i], nav);
      referenceAgents[i] = expectedFrame.CreateEntity();
      expectedFrame.Add(referenceAgents[i], nav);
    }

    actual.Simulation.SaveSnapshot();
    actual.Navigation.AgentSystem.UpdateMovement(ref actualFrame, agents, count, actualFrame.Tick, FP64.Zero);
    actual.Simulation.Rollback(actualFrame.Tick);
    actualFrame = actual.Frame;

    for (var step = 0; step < 5; step++) {
      actual.Navigation.AgentSystem.UpdateMovement(ref actualFrame, agents, count, actualFrame.Tick, FP64.Zero);
      ResolveAllPairs(ref expectedFrame, referenceAgents, expected.Navigation.AgentSystem);
      for (var i = 0; i < count; i++) {
        var actualNav = actualFrame.GetReadOnly<NavAgentComponent>(agents[i]);
        var expectedNav = expectedFrame.GetReadOnly<NavAgentComponent>(referenceAgents[i]);
        actualNav.Position.Should().Be(expectedNav.Position, $"agent {i}, step {step}");
        actualNav.CurrentTriangleIndex.Should().Be(expectedNav.CurrentTriangleIndex);
        actualNav.Status.Should().Be(expectedNav.Status);
        actualNav.Velocity.Should().Be(expectedNav.Velocity);
      }
    }
  }

  // Reference implementation of the original all-pairs correction.
  private static void ResolveAllPairs(ref Frame frame, EntityRef[] agents, FPNavAgentSystem system) {
    var count = Math.Min(agents.Length, system.Tuning.MaxAgents);
    var displacement = new FPVector2[count];
    var weights = new int[count];
    for (var iteration = 0; iteration < system.Tuning.CollisionResolveIterations; iteration++) {
      Array.Clear(displacement);
      Array.Clear(weights);
      for (var a = 0; a < count; a++) {
        ref readonly var first = ref frame.GetReadOnly<NavAgentComponent>(agents[a]);
        for (var b = a + 1; b < count; b++) {
          ref readonly var second = ref frame.GetReadOnly<NavAgentComponent>(agents[b]);
          var radius = first.Radius + second.Radius;
          var difference = first.Position.ToXZ() - second.Position.ToXZ();
          var distanceSquared = difference.sqrMagnitude;
          if (distanceSquared >= radius * radius) continue;
          if (distanceSquared <= FP64.FromRaw(100)) {
            displacement[a] += new FPVector2(-FP64.FromDouble(0.01), FP64.Zero);
            displacement[b] += new FPVector2(FP64.FromDouble(0.01), FP64.Zero);
          }
          else {
            var distance = FP64.Sqrt(distanceSquared);
            var scale = (FP64.One / distance) * ((radius - distance) * FP64.Half) * FP64.FromDouble(0.7);
            var push = difference * scale;
            displacement[a] += push;
            displacement[b] -= push;
          }
          weights[a]++;
          weights[b]++;
        }
      }
      for (var a = 0; a < count; a++) {
        if (weights[a] == 0) continue;
        ref var nav = ref frame.Get<NavAgentComponent>(agents[a]);
        var offset = displacement[a] * (FP64.One / FP64.FromInt(weights[a]));
        var position = nav.Position + new FPVector3(offset.x, FP64.Zero, offset.y);
        var (result, triangle) = system.CurrentQuery.MoveAlongSurface(nav.Position, position,
          nav.CurrentTriangleIndex, FPNavAgentSystem.ResolveWalkMask(nav), system.MultiFloorYThreshold);
        nav.Position = result;
        nav.CurrentTriangleIndex = triangle;
      }
    }
  }
}
