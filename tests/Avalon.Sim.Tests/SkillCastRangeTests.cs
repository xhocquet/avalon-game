using System.Linq;
using FluentAssertions;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using Meesles.Avalon.Sim.Heroes;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using Xunit;

namespace Meesles.Avalon.Sim.Tests;

// MinCastRange/MaxCastRange on the skill row. Both are optional - a row that authors neither keeps
// whatever aim point the client sent, which is what every skill did before the band existed. The
// clamp runs inside the sim, so a modified client aiming across the map casts at its own edge.
//
// The rows here are mutated in place: SimHarness loads its own registry per harness, so a retuned
// row never leaks into another test.
public class SkillCastRangeTests {
  private const int PlayerId = 1;
  private const int Primary = (int)SkillSlot.Primary;

  [Fact]
  public void AnUnboundedRow_LeavesTheAimPointWhereTheClientPutIt() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    var hero = harness.FindHero(PlayerId);
    var skill = SkillProgressionTests.SkillInSlot(harness, hero, SkillSlot.Primary);
    skill.MinCastRange.Should().Be(FP64.Zero);
    skill.MaxCastRange.Should().Be(FP64.Zero);

    var target = HeroPosition(harness) + FPVector3.Right * FP64.FromInt(400);
    var cast = CastAt(harness, target);

    cast.TargetPosition.Should().Be(new FPVector3(target.x, FP64.Zero, target.z));
  }

  [Fact]
  public void AimingPastMaxCastRange_CastsAtTheEdgeOfTheBand() {
    var harness = SimHarness.CreateInitialized();
    Retune(harness, min: 0, max: 10);
    var origin = HeroPosition(harness);

    var cast = CastAt(harness, origin + FPVector3.Right * FP64.FromInt(400));

    // Same aim line, pulled in to the authored reach.
    cast.TargetPosition.Should().Be(new FPVector3(origin.x + FP64.FromInt(10), FP64.Zero, origin.z));
  }

  [Fact]
  public void AimingInsideMinCastRange_PushesTheTargetOutToIt() {
    var harness = SimHarness.CreateInitialized();
    Retune(harness, min: 4, max: 0);
    var origin = HeroPosition(harness);

    var cast = CastAt(harness, origin + FPVector3.Forward * FP64.One);

    cast.TargetPosition.Should().Be(new FPVector3(origin.x, FP64.Zero, origin.z + FP64.FromInt(4)));
  }

  [Fact]
  public void AimingInsideTheBand_IsLeftAlone() {
    var harness = SimHarness.CreateInitialized();
    Retune(harness, min: 2, max: 10);
    var origin = HeroPosition(harness);

    var target = origin + FPVector3.Right * FP64.FromInt(5);
    var cast = CastAt(harness, target);

    cast.TargetPosition.Should().Be(new FPVector3(target.x, FP64.Zero, target.z));
  }

  // A zero-length aim carries no direction to push along, so the min clamp uses the caster's facing -
  // the same fallback a skillshot aimed at its own feet fires down.
  [Fact]
  public void AimingAtYourOwnFeet_PushesOutAlongTheCastersFacing() {
    var harness = SimHarness.CreateInitialized();
    Retune(harness, min: 6, max: 12);
    var frame = harness.Frame;
    var hero = harness.FindHero(PlayerId);
    frame.Get<TransformComponent>(hero).Rotation = FP64.Zero; // yaw 0 faces +Z
    var origin = HeroPosition(harness);

    var cast = CastAt(harness, origin);

    cast.TargetPosition.Should().Be(new FPVector3(origin.x, FP64.Zero, origin.z + FP64.FromInt(6)));
  }

  [Fact]
  public void TheBandIsMeasuredPlanar_SoACasterOffTheGroundPlaneStillReachesTheSameDistance() {
    var harness = SimHarness.CreateInitialized();
    Retune(harness, min: 0, max: 10);
    var frame = harness.Frame;
    var hero = harness.FindHero(PlayerId);
    frame.Get<TransformComponent>(hero).Position.y = FP64.FromInt(3);
    var origin = HeroPosition(harness);

    var cast = CastAt(harness, new FPVector3(origin.x, FP64.Zero, origin.z + FP64.FromInt(50)));

    cast.TargetPosition.Should().Be(new FPVector3(origin.x, FP64.Zero, origin.z + FP64.FromInt(10)));
  }

  private static void Retune(SimHarness harness, int min, int max) {
    var skill = SkillProgressionTests.SkillInSlot(harness, harness.FindHero(PlayerId), SkillSlot.Primary);
    skill.MinCastRange = FP64.FromInt(min);
    skill.MaxCastRange = FP64.FromInt(max);
  }

  private static FPVector3 HeroPosition(SimHarness harness) {
    var frame = harness.Frame;
    return frame.GetReadOnly<TransformComponent>(harness.FindHero(PlayerId)).Position;
  }

  // Learns Primary and casts it at `target` on the current frame, returning the cast event the view
  // (and every effect) reads the aim point off.
  private static SkillCastEvent CastAt(SimHarness harness, FPVector3 target) {
    var frame = harness.Frame;
    var hero = harness.FindHero(PlayerId);
    frame.Get<SkillsComponent>(hero).TrySpendPoint(Primary, 4).Should().BeTrue();

    var collector = new EventCollector();
    collector.BeginTick(frame.Tick);
    frame.EventRaiser = collector;

    SkillActions.TryCast(ref frame, PlayerId, Primary, target).Should().BeTrue();

    return collector.Collected.OfType<SkillCastEvent>().Single();
  }
}
