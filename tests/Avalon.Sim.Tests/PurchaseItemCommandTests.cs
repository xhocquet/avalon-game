using FluentAssertions;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Commands;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Serialization;
using Xunit;

namespace Meesles.Avalon.Sim.Tests;

public class PurchaseItemCommandTests {
  private const int EyeKeyItemId = 300;
  private const int PlayerId = 1;

  [Fact]
  public void Serialize_RoundTripsItemAssetId() {
    var original = new PurchaseItemCommand { PlayerId = 2, Tick = 9, ItemAssetId = 304 };

    var buffer = new byte[original.GetSerializedSize()];
    var writer = new SpanWriter(buffer);
    original.Serialize(ref writer);

    var restored = new PurchaseItemCommand();
    var reader = new SpanReader(buffer);
    restored.Deserialize(ref reader);

    restored.PlayerId.Should().Be(2);
    restored.Tick.Should().Be(9);
    restored.ItemAssetId.Should().Be(304);
  }

  [Fact]
  public void ShopItemAsset_LoadsCostAndAttackBonusFromBytes() {
    var harness = SimHarness.CreateInitialized();
    var item = harness.AssetRegistry.Get<ShopItemAsset>(EyeKeyItemId);

    item.Should().NotBeNull();
    item.Cost.Should().Be(10);
    item.AttackBonus.Should().Be(10, "the .bytes must load AttackBonus, not just Cost");
  }

  [Fact]
  public void Purchase_AppliesAbsoluteStrengthDelta() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;

    var hero = FindHero(ref frame, PlayerId);
    PlaceHeroAtTeamShop(ref frame, hero);
    SetGold(ref frame, hero, 10);
    var strengthBefore = frame.GetReadOnly<StatsComponent>(hero).Strength;

    harness.Tick(Purchase(EyeKeyItemId));

    frame = harness.Frame;
    hero = FindHero(ref frame, PlayerId);
    frame.GetReadOnly<StatsComponent>(hero).Strength.Should().Be(strengthBefore + 10);
    frame.GetReadOnly<InventoryComponent>(hero).Gold.Should().Be(0);
  }

  [Fact]
  public void Purchase_RecordsItemAssetIdInInventory() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;

    var hero = FindHero(ref frame, PlayerId);
    PlaceHeroAtTeamShop(ref frame, hero);
    SetGold(ref frame, hero, 100);

    frame.GetReadOnly<InventoryComponent>(hero).ItemCount.Should().Be(0);

    harness.Tick(Purchase(EyeKeyItemId));
    harness.Tick(Purchase(EyeKeyItemId));

    frame = harness.Frame;
    hero = FindHero(ref frame, PlayerId);
    ref readonly var inventory = ref frame.GetReadOnly<InventoryComponent>(hero);

    // Repeatable buys stack: two purchases append two ledger entries of the same asset id.
    inventory.ItemCount.Should().Be(2);
    inventory.GetItemAssetId(0).Should().Be(EyeKeyItemId);
    inventory.GetItemAssetId(1).Should().Be(EyeKeyItemId);
    inventory.CountOf(EyeKeyItemId).Should().Be(2);
  }

  [Fact]
  public void Purchase_RejectedPurchase_DoesNotRecordItem() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;

    var hero = FindHero(ref frame, PlayerId);
    PlaceHeroFarFromShop(ref frame, hero);
    SetGold(ref frame, hero, 100);

    harness.Tick(Purchase(EyeKeyItemId));

    frame = harness.Frame;
    hero = FindHero(ref frame, PlayerId);
    frame.GetReadOnly<InventoryComponent>(hero).ItemCount.Should().Be(0);
  }

  [Fact]
  public void Purchase_InRangeWithEnoughGold_DeductsGoldAndBuffsStrength() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    var item = harness.AssetRegistry.Get<ShopItemAsset>(EyeKeyItemId);

    var hero = FindHero(ref frame, PlayerId);
    PlaceHeroAtTeamShop(ref frame, hero);
    SetGold(ref frame, hero, item.Cost + 5);
    var strengthBefore = frame.GetReadOnly<StatsComponent>(hero).Strength;

    harness.Tick(Purchase(EyeKeyItemId));

    frame = harness.Frame;
    hero = FindHero(ref frame, PlayerId);
    frame.GetReadOnly<InventoryComponent>(hero).Gold.Should().Be(5);
    frame.GetReadOnly<StatsComponent>(hero).Strength.Should().Be(strengthBefore + item.AttackBonus);
  }

  [Fact]
  public void Purchase_OutOfRange_IsRejected() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    var item = harness.AssetRegistry.Get<ShopItemAsset>(EyeKeyItemId);

    var hero = FindHero(ref frame, PlayerId);
    PlaceHeroFarFromShop(ref frame, hero);
    SetGold(ref frame, hero, 100);
    var strengthBefore = frame.GetReadOnly<StatsComponent>(hero).Strength;

    harness.Tick(Purchase(EyeKeyItemId));

    frame = harness.Frame;
    hero = FindHero(ref frame, PlayerId);
    frame.GetReadOnly<InventoryComponent>(hero).Gold.Should().Be(100);
    frame.GetReadOnly<StatsComponent>(hero).Strength.Should().Be(strengthBefore);
  }

  [Fact]
  public void Purchase_InsufficientGold_IsRejected() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    var item = harness.AssetRegistry.Get<ShopItemAsset>(EyeKeyItemId);

    var hero = FindHero(ref frame, PlayerId);
    PlaceHeroAtTeamShop(ref frame, hero);
    SetGold(ref frame, hero, item.Cost - 1);
    var strengthBefore = frame.GetReadOnly<StatsComponent>(hero).Strength;

    harness.Tick(Purchase(EyeKeyItemId));

    frame = harness.Frame;
    hero = FindHero(ref frame, PlayerId);
    frame.GetReadOnly<InventoryComponent>(hero).Gold.Should().Be(item.Cost - 1);
    frame.GetReadOnly<StatsComponent>(hero).Strength.Should().Be(strengthBefore);
  }

  // CommandValidation's own gate: an id the registry never heard of is dropped before ShopActions runs.
  [Fact]
  public void Purchase_UnknownItemId_IsNotExecuted() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;

    var hero = FindHero(ref frame, PlayerId);
    PlaceHeroAtTeamShop(ref frame, hero);
    SetGold(ref frame, hero, 100);

    harness.Tick(Purchase(999999));

    frame = harness.Frame;
    hero = FindHero(ref frame, PlayerId);
    frame.GetReadOnly<InventoryComponent>(hero).Gold.Should().Be(100);
    frame.GetReadOnly<InventoryComponent>(hero).ItemCount.Should().Be(0);
  }

  // The client gates buys on CanPurchase, so it has to answer exactly what TryPurchase would decide.
  [Theory]
  [InlineData(true, 100, EyeKeyItemId, true)]
  [InlineData(false, 100, EyeKeyItemId, false)]
  [InlineData(true, 0, EyeKeyItemId, false)]
  [InlineData(true, 100, 999999, false)]
  public void CanPurchase_AgreesWithTryPurchase(bool atShop, int gold, int itemId, bool expected) {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;

    var hero = FindHero(ref frame, PlayerId);
    if (atShop)
      PlaceHeroAtTeamShop(ref frame, hero);
    else
      PlaceHeroFarFromShop(ref frame, hero);
    SetGold(ref frame, hero, gold);

    ShopActions.CanPurchase(ref frame, PlayerId, itemId).Should().Be(expected);
    ShopActions.TryPurchase(ref frame, PlayerId, itemId).Should().Be(expected);
  }

  [Fact]
  public void CanPurchase_LeavesTheFrameUntouched() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;

    var hero = FindHero(ref frame, PlayerId);
    PlaceHeroAtTeamShop(ref frame, hero);
    SetGold(ref frame, hero, 100);
    var strengthBefore = frame.GetReadOnly<StatsComponent>(hero).Strength;

    ShopActions.CanPurchase(ref frame, PlayerId, EyeKeyItemId).Should().BeTrue();

    frame.GetReadOnly<InventoryComponent>(hero).Gold.Should().Be(100);
    frame.GetReadOnly<InventoryComponent>(hero).ItemCount.Should().Be(0);
    frame.GetReadOnly<StatsComponent>(hero).Strength.Should().Be(strengthBefore);
  }

  // The client books a queued buy's gold as pending before the sim has run it, so the second click on
  // gold that only covers one item has to be refused locally rather than sent and rejected.
  [Fact]
  public void CanPurchase_CountsPendingGoldAgainstTheWallet() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;

    var hero = FindHero(ref frame, PlayerId);
    PlaceHeroAtTeamShop(ref frame, hero);
    SetGold(ref frame, hero, 15); // one Eye Key at 10, not two

    ShopActions.CanPurchase(ref frame, PlayerId, EyeKeyItemId, 0, 0).Should().BeTrue();
    ShopActions.CanPurchase(ref frame, PlayerId, EyeKeyItemId, 10, 1).Should().BeFalse();
  }

  [Fact]
  public void CanPurchase_CountsPendingItemsAgainstTheLedger() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;

    var hero = FindHero(ref frame, PlayerId);
    PlaceHeroAtTeamShop(ref frame, hero);
    SetGold(ref frame, hero, 10_000);

    ref var inventory = ref frame.Get<InventoryComponent>(hero);
    for (var i = 0; i < InventoryComponent.MaxItems - 1; i++)
      inventory.TryAddItem(EyeKeyItemId);

    ShopActions.CanPurchase(ref frame, PlayerId, EyeKeyItemId, 0, 0).Should().BeTrue();
    ShopActions.CanPurchase(ref frame, PlayerId, EyeKeyItemId, 0, 1).Should().BeFalse();
  }

  private static PurchaseItemCommand Purchase(int itemAssetId) {
    return new PurchaseItemCommand { PlayerId = PlayerId, Tick = 0, ItemAssetId = itemAssetId };
  }

  private static void PlaceHeroAtTeamShop(ref Frame frame, EntityRef hero) {
    ref var transform = ref frame.Get<TransformComponent>(hero);
    transform.Position = TeamShopPosition(ref frame, hero);
  }

  private static void PlaceHeroFarFromShop(ref Frame frame, EntityRef hero) {
    var shopPos = TeamShopPosition(ref frame, hero);
    ref var transform = ref frame.Get<TransformComponent>(hero);
    transform.Position = new FPVector3(shopPos.x + FP64.FromInt(100), shopPos.y, shopPos.z);
  }

  private static FPVector3 TeamShopPosition(ref Frame frame, EntityRef hero) {
    var teamId = frame.GetReadOnly<TeamComponent>(hero).TeamId;
    frame.AssetRegistry.TryGet<MapLayoutAsset>(out var layout).Should().BeTrue();
    layout.TryGetByTypeAndTeam(MapMarkerType.Shop, teamId, out var shopPos).Should().BeTrue();
    return shopPos;
  }

  private static void SetGold(ref Frame frame, EntityRef hero, int gold) {
    ref var inventory = ref frame.Get<InventoryComponent>(hero);
    inventory.Gold = gold;
  }

  private static EntityRef FindHero(ref Frame frame, int playerId) {
    var filter = frame.Filter<Hero, InventoryComponent>();
    while (filter.Next(out var entity))
      if (frame.GetReadOnly<Hero>(entity).PlayerId == playerId)
        return entity;

    Assert.Fail($"No hero found for player {playerId}");
    return default;
  }
}
