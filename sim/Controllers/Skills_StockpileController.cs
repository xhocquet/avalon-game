using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim;

public static unsafe class StockpileController {
  public static void Configure(ref Frame frame, EntityRef entity, SkillAsset skill, int rank) {
    if (skill.StockpileMax <= 0 || rank <= 0)
      return;

    if (!frame.Has<SkillStockpiles>(entity))
      frame.Add(entity, new SkillStockpiles());

    ref var stockpiles = ref frame.Get<SkillStockpiles>(entity);
    var index = stockpiles.Find(skill.AssetId);
    if (index < 0) {
      index = stockpiles.FindFree();
      if (index < 0)
        return;
      stockpiles.SourceIds[index] = skill.AssetId;
      stockpiles.Charges[index] = 0;
    }

    stockpiles.NextRechargeTicks[index] = frame.Tick + RechargeTicks(ref frame, skill, rank);
  }

  public static bool CanSpend(ref Frame frame, EntityRef entity, SkillAsset skill) {
    if (skill.StockpileMax <= 0)
      return true;
    if (!frame.Has<SkillStockpiles>(entity))
      return false;

    ref readonly var stockpiles = ref frame.GetReadOnly<SkillStockpiles>(entity);
    var index = stockpiles.Find(skill.AssetId);
    return index >= 0 && stockpiles.Charges[index] > 0;
  }

  public static bool TrySpend(ref Frame frame, EntityRef entity, SkillAsset skill) {
    if (skill.StockpileMax <= 0)
      return true;
    if (!CanSpend(ref frame, entity, skill))
      return false;

    ref var stockpiles = ref frame.Get<SkillStockpiles>(entity);
    var index = stockpiles.Find(skill.AssetId);
    stockpiles.Charges[index]--;
    if (stockpiles.Charges[index] < skill.StockpileMax && stockpiles.NextRechargeTicks[index] == 0)
      stockpiles.NextRechargeTicks[index] = frame.Tick + RechargeTicks(ref frame, skill,
        RankOf(ref frame, entity, skill.AssetId));
    return true;
  }

  public static void Recharge(ref Frame frame, EntityRef entity) {
    if (!frame.Has<SkillStockpiles>(entity) || !frame.Has<Skills>(entity))
      return;

    ref var stockpiles = ref frame.Get<SkillStockpiles>(entity);
    for (var i = 0; i < SkillStockpiles.MaxEntries; i++) {
      var sourceId = stockpiles.SourceIds[i];
      if (sourceId == 0 || !frame.AssetRegistry.TryGet<SkillAsset>(sourceId, out var skill)) {
        stockpiles.Clear(i);
        continue;
      }

      var rank = RankOf(ref frame, entity, sourceId);
      if (rank <= 0 || skill.StockpileMax <= 0) {
        stockpiles.Clear(i);
        continue;
      }

      var interval = RechargeTicks(ref frame, skill, rank);
      if (stockpiles.Charges[i] >= skill.StockpileMax) {
        stockpiles.Charges[i] = skill.StockpileMax;
        stockpiles.NextRechargeTicks[i] = 0;
        continue;
      }

      if (stockpiles.NextRechargeTicks[i] == 0)
        stockpiles.NextRechargeTicks[i] = frame.Tick + interval;
      if (frame.Tick < stockpiles.NextRechargeTicks[i])
        continue;

      stockpiles.Charges[i]++;
      stockpiles.NextRechargeTicks[i] = stockpiles.Charges[i] >= skill.StockpileMax
        ? 0
        : stockpiles.NextRechargeTicks[i] + interval;
    }
  }

  private static int RankOf(ref Frame frame, EntityRef entity, int sourceId) {
    ref readonly var skills = ref frame.GetReadOnly<Skills>(entity);
    for (var slot = 0; slot < Skills.MaxSlots; slot++)
      if (skills.GetSkillAssetId(slot) == sourceId)
        return skills.GetRank(slot);
    return 0;
  }

  private static int RechargeTicks(ref Frame frame, SkillAsset skill, int rank) {
    var ticks = TickMath.MsToTicksCeil(ref frame,
      SkillAsset.AtRank(skill.StockpileIntervalMs, skill.StockpileIntervalMsPerRank, rank));
    return ticks > 0 ? ticks : 1;
  }
}
