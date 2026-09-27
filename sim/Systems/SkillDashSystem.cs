using System.Collections.Generic;
using Meesles.Avalon.Sim;
using Meesles.Avalon.Sim.Components;
using Meesles.Avalon.Sim.Heroes;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon;

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
      var toDestination = dash.Destination - transform.Position;
      toDestination.y = FP64.Zero;
      var distance = toDestination.magnitude;
      var step = dash.Speed * dt;
      if (step >= distance) {
        transform.Position = dash.Destination;
        _completed.Add(entity);
        continue;
      }

      var move = toDestination.normalized * step;
      transform.Position += move;
      transform.Rotation = FP64.Atan2(move.x, move.z);
    }

    foreach (var entity in _completed) {
      ref readonly var dash = ref frame.GetReadOnly<SkillDash>(entity);
      if (dash.IsActive)
        SkillDashes.Complete(ref frame, entity, in dash, _allies);
      frame.Remove<SkillDash>(entity);
    }
  }
}
