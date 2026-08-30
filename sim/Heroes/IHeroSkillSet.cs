using System;
using System.Collections.Generic;
using Meesles.Avalon.Sim.Assets;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Heroes;

// Everything a skill effect needs about the cast that produced it, resolved once by SkillActions.
// Grows as the cast pipeline does (cast time, resource costs) without re-churning every skill set.
public readonly struct SkillCastContext {
  public readonly EntityRef Caster;
  public readonly int PlayerId;
  public readonly int Slot;
  public readonly SkillAsset Skill;
  public readonly int Rank;
  public readonly FPVector3 CasterPosition;

  // Planar aim point off CastSkillCommand. Self-cast skills ignore it; a skillshot reads it as a
  // direction from CasterPosition.
  public readonly FPVector3 TargetPosition;

  public SkillCastContext(EntityRef caster, int playerId, int slot, SkillAsset skill, int rank,
    FPVector3 casterPosition, FPVector3 targetPosition) {
    Caster = caster;
    PlayerId = playerId;
    Slot = slot;
    Skill = skill;
    Rank = rank;
    CasterPosition = casterPosition;
    TargetPosition = targetPosition;
  }
}

// OnRankGained: Called after a point has been spent and the rank raised
// OnCast: Called when the hero casts the slot, after the cooldown has already been started.
public interface IHeroSkillSet {
  void OnRankGained(ref Frame frame, EntityRef entity, int slot, SkillAsset skill, int newRank);
  void OnCast(ref Frame frame, in SkillCastContext ctx);
}

public delegate void SkillCastHandler(ref Frame frame, in SkillCastContext ctx);

// Slot dispatch for the concrete hero skill sets: each hands the base its four cast methods in
// SkillSlot order, keeping the skill's own name on the method.
public abstract class HeroSkillSetBase : IHeroSkillSet {
  private readonly SkillCastHandler[] _casts;

  protected HeroSkillSetBase(SkillCastHandler primary, SkillCastHandler secondary,
    SkillCastHandler tertiary, SkillCastHandler ultimate) {
    _casts = [primary, secondary, tertiary, ultimate];
  }

  public virtual void OnRankGained(ref Frame frame, EntityRef entity, int slot, SkillAsset skill,
    int newRank) { }

  public void OnCast(ref Frame frame, in SkillCastContext ctx) {
    if ((uint)ctx.Slot >= (uint)_casts.Length)
      return;
    _casts[ctx.Slot]?.Invoke(ref frame, in ctx);
  }
}

public static class HeroSkillSets {
  // Non-deterministic cache
  private static readonly IHeroSkillSet[] Loaded = new IHeroSkillSet[Enum.GetValues<HeroSkillSet>().Length];

  public static IHeroSkillSet Get(int skillSetId) {
    if ((uint)skillSetId >= (uint)Loaded.Length)
      throw new KeyNotFoundException(
        $"HeroAsset names SkillSetId {skillSetId}, which is not a HeroSkillSet value.");

    return Loaded[skillSetId] ??= Create((HeroSkillSet)skillSetId);
  }

  private static IHeroSkillSet Create(HeroSkillSet skillSet) {
    return skillSet switch {
      HeroSkillSet.HairyWizard => new HairyWizardSkills(),
      HeroSkillSet.Snailhead => new SnailheadSkills(),
      HeroSkillSet.CrystalGiant => new CrystalGiantSkills(),
      HeroSkillSet.Skinwalker => new SkinwalkerSkills(),
      HeroSkillSet.PickleKnight => new PickleKnightSkills(),
      _ => throw new KeyNotFoundException(
        $"HeroSkillSet {skillSet} has no implementation in HeroSkillSets.Create.")
    };
  }
}
