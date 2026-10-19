using Meesles.Avalon.Sim.Components;
using Meesles.Avalon.Sim.Assets;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Navigation;

public sealed class UnitSpatialIndex : ISystem {
  public SpatialHashGrid Grid { get; private set; }

  public void Update(ref Frame frame) {
    Grid ??= new SpatialHashGrid(frame.AssetRegistry.Get<CombatRulesAsset>().TargetGridCellSize);
    Grid.Clear();
    var units = frame.Filter<UnitIdentity, Team, Health, TransformComponent>();
    while (units.Next(out var entity))
      Grid.Insert(entity, frame.GetReadOnly<TransformComponent>(entity).Position.ToXZ());
  }
}
