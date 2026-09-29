using FluentAssertions;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using Xunit;

namespace Meesles.Avalon.Sim.Tests;

public class EatToSurviveTests {
  private const int PlayerId = 1;
  private const int Tertiary = (int)SkillSlot.Tertiary;

  [Fact]
  public void Cast_ConsumesAnAccumulatedRationAndRestoresHealthAndMana() {
    var harness = CreateSkinwalkerHarness();
    var hero = harness.FindHero(PlayerId);
    var skill = Asset(harness);
    skill.StockpileIntervalMs = SimHarness.DefaultDeltaTimeMs * 2;
    skill.StockpileIntervalMsPerRank = 0;

    harness.Tick(SimHarness.UpgradeSkillCommand(PlayerId, 0, Tertiary));
    CanSpend(harness, hero, skill).Should().BeFalse();

    TickUntilCharged(harness, hero, skill);
    harness.Frame.Get<Health>(hero).Current = FP64.One;
    harness.Frame.Get<Health>(hero).Mana = FP64.One;
    var healthBefore = harness.Frame.GetReadOnly<Health>(hero).Current;
    var manaBefore = harness.Frame.GetReadOnly<Health>(hero).Mana;

    harness.Tick(SimHarness.CastSkillCommand(PlayerId, 0, Tertiary));

    harness.Frame.GetReadOnly<Health>(hero).Current.Should().Be(
      healthBefore + SkillAsset.AtRank(skill.HealAmount, skill.HealAmountPerRank, 1));
    harness.Frame.GetReadOnly<Health>(hero).Mana.Should().Be(
      manaBefore + SkillAsset.AtRank(skill.ManaRestore, skill.ManaRestorePerRank, 1));
    CanSpend(harness, hero, skill).Should().BeFalse();
  }

  [Fact]
  public void Cast_IsRejectedUntilARationAccumulates() {
    var harness = CreateSkinwalkerHarness();
    var hero = harness.FindHero(PlayerId);
    var skill = Asset(harness);
    skill.StockpileIntervalMs = SimHarness.DefaultDeltaTimeMs * 2;
    skill.StockpileIntervalMsPerRank = 0;

    harness.Tick(SimHarness.UpgradeSkillCommand(PlayerId, 0, Tertiary));
    CastBlock(harness).Should().Be(SkillRejectReason.NoStockpileCharges);

    TickUntilCharged(harness, hero, skill);

    CastBlock(harness).Should().Be(SkillRejectReason.None);
  }

  private static SimHarness CreateSkinwalkerHarness() {
    var harness = SimHarness.CreateInitialized(spawnHeroesNow: false);
    harness.Tick(SimHarness.SelectFactionCommand(1, 0, AssetIds.FactionSkinwalkerTribe),
      SimHarness.SelectFactionCommand(2, 0, AssetIds.FactionSkinwalkerTribe));
    return harness;
  }

  private static void TickUntilCharged(SimHarness harness, EntityRef hero, SkillAsset skill) {
    for (var i = 0; i < 6; i++) {
      if (CanSpend(harness, hero, skill))
        return;
      harness.Tick();
    }

    throw new System.InvalidOperationException("Ration did not accumulate.");
  }

  private static bool CanSpend(SimHarness harness, EntityRef hero, SkillAsset skill) {
    var frame = harness.Frame;
    return StockpileController.CanSpend(ref frame, hero, skill);
  }

  private static SkillRejectReason CastBlock(SimHarness harness) {
    var frame = harness.Frame;
    return SkillsController.CastBlock(ref frame, PlayerId, Tertiary);
  }

  private static SkillAsset Asset(SimHarness harness) =>
    harness.AssetRegistry.Get<SkillAsset>(AssetIds.SkillSkinwalkerTertiary);
}
