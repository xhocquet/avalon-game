using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using Meesles.Avalon.Sim.Factories;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using Xunit;

namespace Meesles.Avalon.Sim.Tests;

// Hairy Wizard's Primary: the single-bullet shape of the volley Crystal Bullets fires three of.
// CrystalBulletsTests owns the shared lifecycle (hit resolution, despawn pairing, kill credit); what
// is checked here is what the row makes different - one fat ball on the aim line, and a cast band.
//
// Dummies are hand-built for the same reason they are there: a real minion would steer out of the way
// mid-flight. They carry exactly what the hit path reads.
public class HairballTests {
  private const int CasterPlayerId = 1;
  private const int CasterTeamId = 1;
  private const int EnemyTeamId = 2;
  private const int Primary = (int)SkillSlot.Primary;

  [Fact]
  public void Cast_PutsOneBallOnTheAimLine() {
    var harness = CreateHarness();
    var skill = HairballAsset(harness);

    var origin = LearnAndCastAlongX(harness);

    var shots = Projectiles(harness);
    shots.Should().ContainSingle();
    shots[0].Component.Index.Should().Be(0);
    shots[0].Component.Direction.Should().Be(FPVector3.Right);

    // A lone bullet has no lateral offset to take, so it rides the aim line exactly.
    shots[0].Position.z.Should().Be(origin.z);
    shots[0].Position.x.Should()
      .Be(origin.x + skill.ProjectileSpawnOffset + StepPerTick(skill));
  }

  // Same rule Crystal Bullets is held to: the whole skill is its one asset row, so a number decided
  // in code fails here rather than quietly shipping.
  [Fact]
  public void EveryProjectileNumber_TracesBackToTheAssetRow() {
    var harness = CreateHarness();
    var skill = HairballAsset(harness);

    LearnAndCastAlongX(harness);

    var shots = Projectiles(harness);
    shots.Should().HaveCount(skill.ProjectileCount);
    shots.Should().OnlyContain(s => s.Component.Speed == skill.ProjectileSpeed);
    shots.Should().OnlyContain(s => s.Component.Radius == skill.ProjectileRadius);
    shots.Should().OnlyContain(s => s.Component.Damage == skill.DamageAtRank(1));
    shots.Should().OnlyContain(s =>
      s.Component.RemainingDistance == skill.ProjectileRange - StepPerTick(skill));
  }

  // The ball is fatter than a crystal bullet, and that is the whole difference on the hit side: an
  // enemy standing off the aim line by more than a bullet's reach still eats it.
  [Fact]
  public void ItsWiderBody_CatchesAnEnemyOffTheAimLine() {
    var harness = CreateHarness();
    var skill = HairballAsset(harness);
    var bullets = harness.AssetRegistry.Get<SkillAsset>(AssetIds.SkillCrystalGiantTertiary);
    skill.ProjectileRadius.Should().BeGreaterThan(bullets.ProjectileRadius);

    var origin = LearnAndCastAlongX(harness);
    var offset = (skill.ProjectileRadius + bullets.ProjectileRadius) / FP64.FromInt(2);
    var enemy = SpawnDummy(harness,
      origin + FPVector3.Right * FP64.FromInt(5) + FPVector3.Forward * offset, EnemyTeamId);
    var healthBefore = harness.Frame.GetReadOnly<Health>(enemy).Current;

    AdvanceUntilTheShotClears(harness, skill);

    harness.Frame.GetReadOnly<Health>(enemy).Current
      .Should().Be(healthBefore - skill.DamageAtRank(1));
  }

  [Fact]
  public void AnEnemyItTouches_StopsIt() {
    var harness = CreateHarness();
    var collector = CollectEvents(harness);

    var origin = LearnAndCastAlongX(harness);
    SpawnDummy(harness, origin + FPVector3.Right * FP64.FromInt(5), EnemyTeamId);

    for (var i = 0; i < 30; i++)
      harness.Tick();

    harness.Count<Projectile>().Should().Be(0);
    var despawned = collector.Collected.OfType<SkillProjectileDespawnedEvent>().ToList();
    despawned.Should().ContainSingle();
    despawned[0].Reason.Should().Be((int)SkillProjectileEnd.Hit);
    despawned[0].HitUnitId.Should().NotBe(0);
  }

  [Fact]
  public void AShotThatHitsNothing_ExpiresAtItsAuthoredRange() {
    var harness = CreateHarness();
    var skill = HairballAsset(harness);

    LearnAndCastAlongX(harness);
    var collector = CollectEvents(harness);
    AdvanceUntilTheShotClears(harness, skill);

    harness.Count<Projectile>().Should().Be(0);
    var despawned = collector.Collected.OfType<SkillProjectileDespawnedEvent>().Single();
    despawned.Reason.Should().Be((int)SkillProjectileEnd.Expired);
  }

  // The row authors a cast band, so an aim past its edge fires along the same line at the edge -
  // SkillAim clamps before the effect sees the point, and the ball dies at range from there.
  [Fact]
  public void AimingPastTheBand_FiresAtItsEdgeRatherThanFurther() {
    var harness = CreateHarness();
    var skill = HairballAsset(harness);
    skill.MaxCastRange.Should().BeGreaterThan(FP64.Zero, "otherwise this proves nothing");

    var collector = CollectEvents(harness);
    var origin = LearnAndCastAlongX(harness, aimDistance: 400);

    var cast = collector.Collected.OfType<SkillCastEvent>().Single();
    cast.TargetPosition.x.Should().Be(origin.x + skill.MaxCastRange);
    Projectiles(harness).Should().ContainSingle();
  }

  // --- setup helpers ---

  // The harness already defaults every player to Hairy Wizards, so no faction-select round trip is
  // needed here. Stripping Combat leaves the hairball as the only thing on the board that can deal
  // damage, so a health delta means exactly one thing.
  private static SimHarness CreateHarness() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    var attackers = new List<EntityRef>();

    var filter = frame.Filter<Combat>();
    while (filter.Next(out var entity))
      attackers.Add(entity);

    foreach (var entity in attackers)
      frame.Remove<Combat>(entity);

    return harness;
  }

  // Returns the caster position the shot actually fired from - read after the upgrade tick, which is
  // what SkillActions sees, and after NavigationAgentSystem has snapped the hero onto the mesh.
  private static FPVector3 LearnAndCastAlongX(SimHarness harness, int aimDistance = 20) {
    harness.Tick(SimHarness.UpgradeSkillCommand(CasterPlayerId, 0, Primary));

    var origin = HeroPosition(harness);
    harness.Tick(SimHarness.CastSkillCommand(CasterPlayerId, 1, Primary,
      origin.x + FP64.FromInt(aimDistance), origin.z));

    return origin;
  }

  private static FP64 StepPerTick(SkillAsset skill) {
    return skill.ProjectileSpeed * (FP64.FromInt(SimHarness.DefaultDeltaTimeMs) / FP64.FromInt(1000));
  }

  private static void AdvanceUntilTheShotClears(SimHarness harness, SkillAsset skill) {
    var ticks = (skill.ProjectileRange / StepPerTick(skill)).ToInt() + 4;
    for (var i = 0; i < ticks; i++)
      harness.Tick();
  }

  private static EntityRef SpawnDummy(SimHarness harness, FPVector3 position, int teamId) {
    var frame = harness.Frame;
    var entity = frame.CreateEntity();

    frame.Add(entity, TransformFactory.At(position));
    frame.Add(entity, new UnitIdComponent {
      UnitId = UnitLookup.NextUnitId(ref frame),
      UnitTypeId = SimulationSetup.MinionUnitTypeId
    });
    frame.Add(entity, new TeamComponent(teamId));
    frame.Add(entity, new Health(500));
    frame.Add(entity, new Minion { WaveId = 0 });

    return entity;
  }

  private static EventCollector CollectEvents(SimHarness harness) {
    var collector = new EventCollector();
    collector.BeginTick(harness.Frame.Tick);
    harness.Frame.EventRaiser = collector;
    return collector;
  }

  // --- readers ---

  private static SkillAsset HairballAsset(SimHarness harness) {
    return harness.AssetRegistry.Get<SkillAsset>(AssetIds.SkillHairyWizardPrimary);
  }

  private static FPVector3 HeroPosition(SimHarness harness) {
    return harness.Frame.GetReadOnly<TransformComponent>(harness.FindHero(CasterPlayerId)).Position;
  }

  private static List<(Projectile Component, FPVector3 Position)> Projectiles(SimHarness harness) {
    var frame = harness.Frame;
    var found = new List<(Projectile, FPVector3)>();

    var filter = frame.Filter<Projectile, TransformComponent>();
    while (filter.Next(out var entity))
      found.Add((frame.GetReadOnly<Projectile>(entity), frame.GetReadOnly<TransformComponent>(entity).Position));

    return found.OrderBy(p => p.Item1.Index).ToList();
  }
}
