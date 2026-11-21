using System.Collections.Generic;
using FluentAssertions;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using Meesles.Avalon.Sim.Factories;
using Meesles.Avalon.Sim.Heroes;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using Xunit;

namespace Meesles.Avalon.Sim.Tests;

public class TrainingDummyTests {
  [Fact]
  public void Ally_ReceivesFriendlyBuffs_WhileEnemyDoesNot() {
    var harness = SimHarness.CreateInitialized(maxPlayers: 1);
    var frame = harness.Frame;
    var hero = harness.FindHero(1);
    var origin = frame.GetReadOnly<TransformComponent>(hero).Position;
    var position = origin + new FPVector3(FP64.One, FP64.Zero, FP64.Zero);
    var ally = TrainingDummyFactory.Spawn(ref frame, position, 1, AssetIds.FactionPickleKnights,
      TrainingDummyFactory.AllyPlayerId);
    var enemy = TrainingDummyFactory.Spawn(ref frame,
      position + new FPVector3(FP64.One, FP64.Zero, FP64.Zero), 2, AssetIds.FactionCrystalWarriors);
    var armor = frame.GetReadOnly<Stats>(ally).Armor;
    var resistance = frame.GetReadOnly<Stats>(ally).MagicResist;
    var skill = frame.AssetRegistry.Get<SkillAsset>(AssetIds.SkillSnailheadTertiary);
    var context = new SkillCastContext(hero, 1, 2, skill, 1, origin, position);
    new SnailheadSkills().OnCast(ref frame, in context);
    frame.GetReadOnly<Stats>(ally).Armor.Should().BeGreaterThan(armor);
    frame.GetReadOnly<Stats>(ally).MagicResist.Should().BeGreaterThan(resistance);
    frame.Has<Buffs>(enemy).Should().BeFalse();
    frame.GetReadOnly<Stats>(enemy).Armor.Should().Be(armor);
    frame.Has<Combat>(ally).Should().BeFalse();
    frame.Has<Controllable>(ally).Should().BeFalse();
  }

  [Fact]
  public void Dummy_TakesAttacksAndSpells_WithoutMovingOrRetaliating() {
    var harness = SimHarness.CreateInitialized(maxPlayers: 1);
    var frame = harness.Frame;
    var hero = harness.FindHero(1);
    var origin = frame.GetReadOnly<TransformComponent>(hero).Position;
    var position = origin + new FPVector3(FP64.One, FP64.Zero, FP64.Zero);
    var dummy = TrainingDummyFactory.Spawn(ref frame, position, 2, AssetIds.FactionCrystalWarriors);
    UnitLookup.TryGetPlayerHero(ref frame, 0, out _).Should().BeFalse();
    var activeTeams = new List<int>();
    TeamRegistry.CollectActiveTeams(ref frame, activeTeams);
    activeTeams.Should().Equal(1);
    frame.GetReadOnly<Health>(dummy).Current.Should().Be(FP64.FromInt(20000));
    var heroHealth = frame.GetReadOnly<Health>(hero).Current;
    var targetId = UnitLookup.GetUnitId(ref frame, dummy);
    harness.Tick(SimHarness.AttackCommand(1, frame.Tick, targetId, UnitLookup.GetUnitId(ref frame, hero)));
    for (var i = 0; i < 120; i++) harness.Tick();
    frame = harness.Frame;
    frame.GetReadOnly<Health>(dummy).Current.Should().BeLessThan(FP64.FromInt(20000));
    frame.GetReadOnly<Health>(hero).Current.Should().Be(heroHealth);
    frame.GetReadOnly<TransformComponent>(dummy).Position.Should().Be(position);
    frame.Has<Combat>(dummy).Should().BeFalse();
    frame.Has<Controllable>(dummy).Should().BeFalse();

    var beforeSpell = frame.GetReadOnly<Health>(dummy).Current;
    var casterPosition = frame.GetReadOnly<TransformComponent>(hero).Position;
    var cone = frame.AssetRegistry.Get<SkillAsset>(AssetIds.SkillSnailheadPrimary);
    var coneContext = new SkillCastContext(hero, 1, 0, cone, 1, casterPosition, position);
    new SnailheadSkills().OnCast(ref frame, in coneContext);
    frame.GetReadOnly<Health>(dummy).Current.Should().BeLessThan(beforeSpell);

    var silence = frame.AssetRegistry.Get<SkillAsset>(AssetIds.SkillPickleKnightUltimate);
    var silenceContext = new SkillCastContext(hero, 1, 3, silence, 1, casterPosition, position);
    new PickleKnightSkills().OnCast(ref frame, in silenceContext);
    frame.GetReadOnly<Silence>(dummy).IsSilenced.Should().BeTrue();
  }
}
