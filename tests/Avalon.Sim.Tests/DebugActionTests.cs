using FluentAssertions;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using Xunit;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Tests;

public class DebugActionTests {
  [Fact]
  public void SwitchFaction_RespawnsTheHeroFromTheNewFaction() {
    // Deferred spawn path: the PlayerFaction slot only exists when heroes wait on a pick, and the
    // slot is what SwitchFaction rewrites.
    var harness = SimHarness.CreateInitialized(spawnHeroesNow: false);
    harness.Tick(SimHarness.SelectFactionCommand(1, harness.Frame.Tick, AssetIds.FactionHairyWizards));
    harness.Tick();

    var before = harness.FindHero(1);
    harness.Frame.GetReadOnly<Faction>(before).FactionId.Should().Be(AssetIds.FactionHairyWizards);

    harness.Tick(SimHarness.DebugCommand(1, harness.Frame.Tick, DebugAction.SwitchFaction,
      AssetIds.FactionPickleKnights));
    harness.Tick();

    var after = harness.FindHero(1);
    harness.Count<Hero>().Should().Be(1); // The old hero is gone, not left standing beside the new one
    harness.Frame.GetReadOnly<Faction>(after).FactionId.Should().Be(AssetIds.FactionPickleKnights);
    harness.Frame.GetReadOnly<Hero>(after).HeroAssetId.Should()
      .NotBe(harness.Frame.AssetRegistry.Get<FactionAsset>(AssetIds.FactionHairyWizards).HeroAssetId);
  }

  [Fact]
  public void SwitchFaction_UnknownFactionLeavesTheHeroAlone() {
    var harness = SimHarness.CreateInitialized(spawnHeroesNow: false);
    harness.Tick(SimHarness.SelectFactionCommand(1, harness.Frame.Tick, AssetIds.FactionHairyWizards));
    harness.Tick();

    harness.Tick(SimHarness.DebugCommand(1, harness.Frame.Tick, DebugAction.SwitchFaction, 999999));
    harness.Tick();

    harness.Frame.GetReadOnly<Faction>(harness.FindHero(1)).FactionId
      .Should().Be(AssetIds.FactionHairyWizards);
  }

  [Fact]
  public void AddGold_CreditsTheIssuingPlayerOnly() {
    var harness = SimHarness.CreateInitialized();
    var before = harness.Frame.GetReadOnly<Inventory>(harness.FindHero(1)).Gold;
    var otherBefore = harness.Frame.GetReadOnly<Inventory>(harness.FindHero(2)).Gold;

    harness.Tick(SimHarness.DebugCommand(1, harness.Frame.Tick, DebugAction.AddGold, 750));

    harness.Frame.GetReadOnly<Inventory>(harness.FindHero(1)).Gold.Should().Be(before + 750);
    harness.Frame.GetReadOnly<Inventory>(harness.FindHero(2)).Gold.Should().Be(otherBefore);
  }

  [Fact]
  public void AddExperience_LevelsTheHeroThroughExperienceSystem() {
    var harness = SimHarness.CreateInitialized();
    harness.Frame.GetReadOnly<Experience>(harness.FindHero(1)).Level.Should().Be(1);

    harness.Tick(SimHarness.DebugCommand(1, harness.Frame.Tick, DebugAction.AddExperience, 100000));

    harness.Frame.GetReadOnly<Experience>(harness.FindHero(1)).Level.Should().BeGreaterThan(1);
  }

  [Fact]
  public void MaxSkills_RanksEverySlotToItsCap() {
    var harness = SimHarness.CreateInitialized();

    harness.Tick(SimHarness.DebugCommand(1, harness.Frame.Tick, DebugAction.MaxSkills));

    var frame = harness.Frame;
    var hero = harness.FindHero(1);
    ref readonly var skills = ref frame.GetReadOnly<Skills>(hero);
    for (var slot = 0; slot < Skills.MaxSlots; slot++) {
      if (!frame.AssetRegistry.TryGet<SkillAsset>(skills.GetSkillAssetId(slot), out var skill))
        continue;

      skills.GetRank(slot).Should().Be(skill.MaxRank);
    }
  }

  [Fact]
  public void RefreshCooldowns_ClearsACooldownACastStarted() {
    var harness = SimHarness.CreateInitialized();
    harness.Tick(SimHarness.DebugCommand(1, harness.Frame.Tick, DebugAction.MaxSkills));
    harness.Tick(SimHarness.CastSkillCommand(1, harness.Frame.Tick, 0));
    harness.Frame.GetReadOnly<Skills>(harness.FindHero(1)).GetCooldownRemainingTicks(0)
      .Should().BeGreaterThan(0);

    harness.Tick(SimHarness.DebugCommand(1, harness.Frame.Tick, DebugAction.RefreshCooldowns));

    harness.Frame.GetReadOnly<Skills>(harness.FindHero(1)).GetCooldownRemainingTicks(0)
      .Should().Be(0);
  }

  [Fact]
  public void KillHero_ZeroesHealthAndTheHeroComesBack() {
    var harness = SimHarness.CreateInitialized();

    harness.Tick(SimHarness.DebugCommand(1, harness.Frame.Tick, DebugAction.KillHero));

    var hero = harness.FindHero(1); // Destroyed heroes never come back - a killed one is still there
    harness.Frame.GetReadOnly<Health>(hero).IsAlive.Should().BeFalse();
    harness.Frame.Has<PendingRespawn>(hero).Should().BeTrue();
  }

  [Fact]
  public void HealFull_RestoresAWoundedHero() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    var hero = harness.FindHero(1);
    var full = frame.GetReadOnly<Health>(hero).Current;
    DamageApplication.ApplyDamage(ref frame, harness.FindHero(2), hero, FP64.FromInt(20));
    harness.Frame.GetReadOnly<Health>(hero).Current.Should().BeLessThan(full);

    harness.Tick(SimHarness.DebugCommand(1, harness.Frame.Tick, DebugAction.HealFull));

    harness.Frame.GetReadOnly<Health>(hero).Current.Should().Be(full);
  }

  [Fact]
  public void SpawnMinions_PutsThemOnAnEnemyTeamAtTheTarget() {
    var harness = SimHarness.CreateInitialized();
    var before = harness.Count<Minion>();

    harness.Tick(SimHarness.DebugCommand(1, harness.Frame.Tick, DebugAction.SpawnMinions, 0,
      FP64.FromInt(5), FP64.FromInt(5)));

    harness.Count<Minion>().Should().BeGreaterThan(before);
    var frame = harness.Frame;
    var filter = frame.Filter<Minion, Team>();
    while (filter.Next(out var entity))
      frame.GetReadOnly<Team>(entity).TeamId.Should().NotBe(1);
  }

  // The view resolves a minion's model through the faction, and on a playground the target team has no
  // PlayerFaction slot to read one from - an unstamped minion made the view throw once per tick.
  [Fact]
  public void SpawnMinions_StampsAFactionOnEveryMinion() {
    var harness = SimHarness.CreateInitialized();

    harness.Tick(SimHarness.DebugCommand(1, harness.Frame.Tick, DebugAction.SpawnMinions, 3));

    var frame = harness.Frame;
    var seen = 0;
    var filter = frame.Filter<Minion>();
    while (filter.Next(out var entity)) {
      frame.Has<Faction>(entity).Should().BeTrue();
      frame.GetReadOnly<Faction>(entity).FactionId.Should().BeGreaterThan(0);
      seen++;
    }

    seen.Should().BeGreaterThan(0);
  }

  [Fact]
  public void SpawnMinions_HonoursAnExplicitFaction() {
    var harness = SimHarness.CreateInitialized();

    harness.Tick(SimHarness.DebugCommand(1, harness.Frame.Tick, DebugAction.SpawnMinions, 3,
      FP64.Zero, FP64.Zero, AssetIds.FactionSkinwalkerTribe));

    var frame = harness.Frame;
    var filter = frame.Filter<Minion, Faction>();
    while (filter.Next(out var entity))
      frame.GetReadOnly<Faction>(entity).FactionId.Should().Be(AssetIds.FactionSkinwalkerTribe);
  }

  [Fact]
  public void SpawnMinions_UnknownFactionIsRejected() {
    var harness = SimHarness.CreateInitialized();
    var before = harness.Count<Minion>();

    harness.Tick(SimHarness.DebugCommand(1, harness.Frame.Tick, DebugAction.SpawnMinions, 3,
      FP64.Zero, FP64.Zero, 999999));

    harness.Count<Minion>().Should().Be(before);
  }

  [Fact]
  public void ClearMinions_RemovesEveryTeamsMinions() {
    var harness = SimHarness.CreateInitialized();
    harness.Tick(SimHarness.DebugCommand(1, harness.Frame.Tick, DebugAction.SpawnMinions, 2));
    harness.Count<Minion>().Should().BeGreaterThan(0);

    harness.Tick(SimHarness.DebugCommand(1, harness.Frame.Tick, DebugAction.ClearMinions));

    harness.Count<Minion>().Should().Be(0);
  }

  [Fact]
  public void TeleportHero_MovesTheHeroToTheTarget() {
    var harness = SimHarness.CreateInitialized();

    harness.Tick(SimHarness.DebugCommand(1, harness.Frame.Tick, DebugAction.TeleportHero, 0,
      FP64.FromInt(7), FP64.FromInt(-3)));

    var position = harness.Frame.GetReadOnly<TransformComponent>(harness.FindHero(1)).Position;
    position.x.Should().Be(FP64.FromInt(7));
    position.z.Should().Be(FP64.FromInt(-3));
  }

  [Fact]
  public void UnknownAction_IsRejectedBeforeItReachesTheHandler() {
    var harness = SimHarness.CreateInitialized();
    var before = harness.Frame.GetReadOnly<Inventory>(harness.FindHero(1)).Gold;

    harness.Tick(SimHarness.DebugCommand(1, harness.Frame.Tick, (DebugAction)9999, 500));

    harness.Frame.GetReadOnly<Inventory>(harness.FindHero(1)).Gold.Should().Be(before);
  }
}
