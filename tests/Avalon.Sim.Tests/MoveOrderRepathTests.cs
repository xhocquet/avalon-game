using FluentAssertions;
using Meesles.Avalon.Sim.Components;
using Xunit;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.Deterministic.Navigation;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Tests;

// Re-ordering a unit that is already walking must not stall it.
//
// NavAgentComponent.SetDestination drops the current path, and the agent's PathRepathCooldown then
// refuses to build the replacement for up to 10 ticks - so the unit stood still, with no path and no
// way to get one, until the window closed. UnitIntent.AllowImmediateRepath exempts player orders.
public class MoveOrderRepathTests {
  private const int FirstTarget = 20;
  private const int SecondTargetX = -20;
  private const int SecondTargetZ = -30;

  [Fact]
  public void ReorderWhileMoving_KeepsMakingProgress() {
    var harness = SimHarness.CreateInitialized();
    StartMoving(harness);

    // Re-order well inside PathRepathCooldown, which is where the stall used to live.
    harness.Tick(SimHarness.MoveCommand(1, 5, FP64.FromInt(SecondTargetX), FP64.FromInt(SecondTargetZ)));

    var before = HeroPosition(harness);
    harness.Tick();
    harness.Tick();
    var after = HeroPosition(harness);

    (after - before).sqrMagnitude.Should().NotBe(FP64.Zero, "the unit should keep walking through a re-order");
  }

  [Fact]
  public void ReorderWhileMoving_RepathsImmediately() {
    var harness = SimHarness.CreateInitialized();
    StartMoving(harness);

    harness.Tick(SimHarness.MoveCommand(1, 5, FP64.FromInt(SecondTargetX), FP64.FromInt(SecondTargetZ)));

    var nav = HeroNav(harness);
    nav.Status.Should().Be((byte)FPNavAgentStatus.Moving);
    nav.HasPath.Should().BeTrue("the replacement path should be built on the same tick the order lands");
  }

  // The exemption is spent on the order itself, not left open: the repath re-arms the cooldown from
  // the order's own tick, so chase retargeting after it still pays the normal rate limit.
  [Fact]
  public void MoveOrder_RearmsCooldownFromTheOrderTick() {
    var harness = SimHarness.CreateInitialized();
    StartMoving(harness);
    var orderTick = harness.Frame.Tick;

    harness.Tick(SimHarness.MoveCommand(1, 5, FP64.FromInt(SecondTargetX), FP64.FromInt(SecondTargetZ)));

    HeroNav(harness).LastRepathTick.Should().Be(orderTick,
      "the path is rebuilt on the tick the order lands, and the cooldown restarts from there");
  }

  // The cooldown check exempts LastRepathTick == 0, so the first order has to land after tick 0 or the
  // agent never arms the cooldown and the scenario under test cannot occur.
  private static void StartMoving(SimHarness harness) {
    harness.Tick();
    harness.Tick(SimHarness.MoveCommand(1, 1, FP64.FromInt(FirstTarget), FP64.Zero));
    for (var i = 0; i < 3; i++)
      harness.Tick();

    var nav = HeroNav(harness);
    nav.Status.Should().Be((byte)FPNavAgentStatus.Moving, "the first order should be under way");
    nav.LastRepathTick.Should().BeGreaterThan(0, "the cooldown must be armed for this scenario to mean anything");
  }

  private static NavAgentComponent HeroNav(SimHarness harness) {
    var frame = harness.Frame;
    UnitLookup.TryGetPlayerHero(ref frame, 1, out var hero).Should().BeTrue();
    return frame.GetReadOnly<NavAgentComponent>(hero);
  }

  private static FPVector3 HeroPosition(SimHarness harness) {
    var frame = harness.Frame;
    UnitLookup.TryGetPlayerHero(ref frame, 1, out var hero).Should().BeTrue();
    return frame.GetReadOnly<TransformComponent>(hero).Position;
  }
}
