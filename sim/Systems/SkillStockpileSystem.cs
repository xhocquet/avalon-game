using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim;

public sealed class SkillStockpileSystem : ISystem {
  public void Update(ref Frame frame) {
    var stockpiles = frame.Filter<SkillStockpiles>();
    while (stockpiles.Next(out var entity))
      StockpileController.Recharge(ref frame, entity);
  }
}
