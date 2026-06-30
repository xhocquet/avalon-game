using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using Meesles.Avalon.Sim;
using Meesles.Avalon.Sim.Models;
using Meesles.Avalon.Sim.Assets;

namespace Meesles.Avalon {
  public class ScoreSystem : ISystem {
    public void Update(ref Frame frame) {
      var stats = frame.AssetRegistry.Get<PlayerStatsAsset>();
      int matchDurationMs = (stats.MatchDuration * FP64.FromInt(1000)).ToInt();
      int matchEndTick = matchDurationMs / frame.DeltaTimeMs;
      if (frame.Tick != matchEndTick) return;

      var evt = EventPool.Get<GameOverEvent>();
      frame.EventRaiser.RaiseEvent(evt);
    }
  }
}
