using System.Collections.Generic;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim;

public class SkillDashSystem : ISystem {
  private readonly List<EntityRef> _completed = [];
  private readonly List<EntityRef> _allies = [];

  public void Update(ref Frame frame) {
    var dt = FP64.FromInt(TickMath.DeltaTimeMs(ref frame)) / FP64.FromInt(1000);
    _completed.Clear();

    var dashing = frame.Filter<SkillDash, TransformComponent>();
    while (dashing.Next(out var entity)) {
      ref readonly var dash = ref frame.GetReadOnly<SkillDash>(entity);
      if (!dash.IsActive) {
        _completed.Add(entity);
        continue;
      }

      ref var transform = ref frame.Get<TransformComponent>(entity);
      var step = dash.Speed * dt;
      if (Planar.MoveTowards(ref transform, dash.Destination, step)) {
        _completed.Add(entity);
        continue;
      }
    }

    foreach (var entity in _completed) {
      ref readonly var dash = ref frame.GetReadOnly<SkillDash>(entity);
      if (dash.IsActive)
        DashController.Complete(ref frame, entity, in dash, _allies);
      frame.Remove<SkillDash>(entity);
    }
  }
}
