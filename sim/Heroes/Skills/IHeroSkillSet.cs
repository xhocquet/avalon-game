using Meesles.Avalon.Sim.Assets;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Heroes.Skills;

// Per-hero skill logic, selected by HeroAsset.SkillSetId through HeroSkillSets.Get. One implementation
// per hero, each owning all four of that hero's slots, so a skill is implemented by editing one method
// in one file with no shared code to fork.
//
// Implementations are stateless singletons. A field here would survive a rollback and desync the
// client - all skill state belongs on SkillsComponent or another component.
public interface IHeroSkillSet {
  // Called after a point has been spent and the rank raised. newRank is the rank now in effect.
  void OnRankGained(ref Frame frame, EntityRef entity, int slot, SkillAsset skill, int newRank);

  // Called when the hero casts the slot, after the cooldown has already been started.
  void OnCast(ref Frame frame, EntityRef entity, int slot, SkillAsset skill, int rank);
}
