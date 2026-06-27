using Meesles.Avalon.Sim.Models;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon {
  public class AttackCooldownSystem : ISystem {
    public void Update(ref Frame frame) {
      var filter = frame.Filter<Combat>();
      while (filter.Next(out var entity)) {
        ref var combat = ref frame.Get<Combat>(entity);
        if (combat.CooldownRemainingTicks > 0)
          combat.CooldownRemainingTicks--;
      }
    }
  }
}
