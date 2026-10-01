using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using Meesles.Avalon.Sim.Heroes;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim;

// Handles skill casts and upgrades after command validation
public static class SkillsController {
  // Spends one skill point to raise a slot's rank
  public static bool TryUpgrade(ref Frame frame, int playerId, int slot) {
    var block = EvaluateUpgrade(ref frame, playerId, slot, out var heroEntity, out var heroAsset, out var skill);
    if (block != SkillRejectReason.None) {
      Reject(ref frame, "Upgrade", playerId, slot, Describe(ref frame, block, heroEntity, slot, skill));
      return false;
    }

    ref var skills = ref frame.Get<Skills>(heroEntity);
    skills.TrySpendPoint(slot, skill.MaxRank);
    var newRank = skills.GetRank(slot);
    var remainingPoints = skills.SkillPoints;

    HeroSkillSets.Get(heroAsset.SkillSetId).OnRankGained(ref frame, heroEntity, slot, skill, newRank);
    StockpileController.Configure(ref frame, heroEntity, skill, newRank);
    RaiseUpgradedEvent(ref frame, heroEntity, playerId, slot, skills.GetSkillAssetId(slot), newRank,
      remainingPoints);

    SimLog.Info(ref frame,
      $"[Skills] event=upgraded tick={frame.Tick} playerId={playerId} slot={slot} skillAssetId={skills.GetSkillAssetId(slot)} rank={newRank} pointsLeft={remainingPoints}");
    return true;
  }

  // Clamps ground targets to cast range; self-casts use the caster position
  public static bool TryCast(ref Frame frame, int playerId, int slot, FPVector3 target) {
    var block = EvaluateCast(ref frame, playerId, slot, out var heroEntity, out var heroAsset, out var skill);
    if (block != SkillRejectReason.None) {
      Reject(ref frame, "Cast", playerId, slot, Describe(ref frame, block, heroEntity, slot, skill));
      return false;
    }

    ref var skills = ref frame.Get<Skills>(heroEntity);

    var continuingDash = DashController.CanContinue(ref frame, heroEntity, skill.AssetId);
    var cooldownTicks = CooldownTicks(ref frame, skill);
    if (!continuingDash)
      skills.StartCooldown(slot, cooldownTicks);

    var rank = skills.GetRank(slot);
    var skillAssetId = skills.GetSkillAssetId(slot);

    // EvaluateCast already checked the mana pool
    ManaController.TrySpend(ref frame, heroEntity, skill.ManaCostAtRank(rank));
    StockpileController.TrySpend(ref frame, heroEntity, skill);

    var casterPosition = frame.Has<TransformComponent>(heroEntity)
      ? frame.GetReadOnly<TransformComponent>(heroEntity).Position
      : FPVector3.Zero;
    // Self-casts ignore the requested target
    target = skill.SelfCast
      ? casterPosition
      : SkillAim.ClampToCastRange(ref frame, heroEntity, skill, casterPosition, target);

    var ctx = new SkillCastContext(heroEntity, playerId, slot, skill, rank, casterPosition, target);
    HeroSkillSets.Get(heroAsset.SkillSetId).OnCast(ref frame, in ctx);
    RaiseCastEvent(ref frame, in ctx, skillAssetId);

    SimLog.Info(ref frame,
      $"[Skills] event=cast tick={frame.Tick} playerId={playerId} slot={slot} skillAssetId={skillAssetId} rank={rank} cooldownTicks={cooldownTicks} target=({target.x},{target.z})");
    return true;
  }

  // Read-only cast verdict; pending ranks include queued upgrades
  public static SkillRejectReason CastBlock(ref Frame frame, int playerId, int slot, int pendingRanks = 0) {
    return EvaluateCast(ref frame, playerId, slot, out _, out _, out _, pendingRanks);
  }

  // Read-only upgrade verdict; pending values include queued upgrades
  public static SkillRejectReason UpgradeBlock(ref Frame frame, int playerId, int slot, int pendingPoints = 0,
    int pendingRanks = 0) {
    return EvaluateUpgrade(ref frame, playerId, slot, out _, out _, out _, pendingPoints, pendingRanks);
  }

  public static bool CanCast(ref Frame f, int playerId, int slot) =>
    CastBlock(ref f, playerId, slot) == SkillRejectReason.None;

  public static bool CanCast(ref Frame f, int playerId, int slot, int pendingRanks) =>
    CastBlock(ref f, playerId, slot, pendingRanks) == SkillRejectReason.None;

  public static bool CanUpgrade(ref Frame f, int playerId, int slot, int pointsPend, int ranksPend) =>
    UpgradeBlock(ref f, playerId, slot, pointsPend, ranksPend) == SkillRejectReason.None;


  // Read-only cast validation
  private static SkillRejectReason EvaluateCast(ref Frame frame, int playerId, int slot,
    out EntityRef heroEntity, out HeroStatsAsset heroAsset, out SkillAsset skill, int pendingRanks = 0) {
    var block = Resolve(ref frame, playerId, slot, out heroEntity, out heroAsset, out skill);
    if (block != SkillRejectReason.None)
      return block;

    if (!frame.Has<Health>(heroEntity) || !frame.GetReadOnly<Health>(heroEntity).IsAlive)
      return SkillRejectReason.HeroDead;

    if (SilenceController.IsSilenced(ref frame, heroEntity))
      return SkillRejectReason.Silenced;

    ref readonly var skills = ref frame.GetReadOnly<Skills>(heroEntity);
    var rank = skills.GetRank(slot) + pendingRanks;
    if (rank <= 0)
      return SkillRejectReason.NotLearned;

    if (skills.GetCooldownRemainingTicks(slot) > 0 &&
        !DashController.CanContinue(ref frame, heroEntity, skill.AssetId))
      return SkillRejectReason.OnCooldown;

    if (!StockpileController.CanSpend(ref frame, heroEntity, skill))
      return SkillRejectReason.NoStockpileCharges;

    return ManaController.CanAfford(ref frame, heroEntity, skill.ManaCostAtRank(rank))
      ? SkillRejectReason.None
      : SkillRejectReason.NotEnoughMana;
  }

  private static SkillRejectReason EvaluateUpgrade(ref Frame frame, int playerId, int slot,
    out EntityRef heroEntity, out HeroStatsAsset heroAsset, out SkillAsset skill,
    int pendingPoints = 0, int pendingRanks = 0) {
    var block = Resolve(ref frame, playerId, slot, out heroEntity, out heroAsset, out skill);
    if (block != SkillRejectReason.None)
      return block;

    ref readonly var skills = ref frame.GetReadOnly<Skills>(heroEntity);
    if (skills.SkillPoints - pendingPoints <= 0)
      return SkillRejectReason.NoSkillPoints;

    return skills.GetRank(slot) + pendingRanks >= skill.MaxRank ? SkillRejectReason.AtMaxRank : SkillRejectReason.None;
  }

  // Resolves the player's hero, asset row, and slotted skill
  private static SkillRejectReason Resolve(ref Frame frame, int playerId, int slot,
    out EntityRef heroEntity, out HeroStatsAsset heroAsset, out SkillAsset skill) {
    heroAsset = null;
    skill = null;

    if (!UnitLookup.TryGetPlayerHero(ref frame, playerId, out heroEntity))
      return SkillRejectReason.NoHero;

    if (!frame.Has<Skills>(heroEntity))
      return SkillRejectReason.HeroMissingSkills;

    var skillAssetId = frame.GetReadOnly<Skills>(heroEntity).GetSkillAssetId(slot);
    if (!frame.AssetRegistry.TryGet<SkillAsset>(skillAssetId, out skill))
      return SkillRejectReason.SkillAssetMissing;

    var heroAssetId = frame.GetReadOnly<Hero>(heroEntity).HeroStatsAssetId;
    return frame.AssetRegistry.TryGet<HeroStatsAsset>(heroAssetId, out heroAsset)
      ? SkillRejectReason.None
      : SkillRejectReason.HeroStatsAssetMissing;
  }

  // Formats rejection reasons
  private static string Describe(ref Frame frame, SkillRejectReason block, EntityRef heroEntity, int slot,
    SkillAsset skill) {
    switch (block) {
      case SkillRejectReason.NoHero: return "no_hero_for_player";
      case SkillRejectReason.HeroMissingSkills: return "hero_missing_skills";
      case SkillRejectReason.HeroDead: return "hero_dead";
      case SkillRejectReason.Silenced: return "silenced";
      case SkillRejectReason.NotLearned: return "skill_not_learned";
      case SkillRejectReason.NoStockpileCharges: return "no_stockpile_charges";
      case SkillRejectReason.AtMaxRank: return $"skill_at_max_rank maxRank={skill.MaxRank}";
      case SkillRejectReason.HeroStatsAssetMissing:
        return $"hero_stats_asset_missing heroId={frame.GetReadOnly<Hero>(heroEntity).HeroStatsAssetId}";
    }

    ref readonly var skills = ref frame.GetReadOnly<Skills>(heroEntity);
    return block switch {
      SkillRejectReason.SkillAssetMissing => $"skill_asset_missing skillId={skills.GetSkillAssetId(slot)}",
      SkillRejectReason.OnCooldown => $"on_cooldown remainingTicks={skills.GetCooldownRemainingTicks(slot)}",
      SkillRejectReason.NoSkillPoints => $"no_skill_points rank={skills.GetRank(slot)}",
      SkillRejectReason.NotEnoughMana =>
        $"not_enough_mana cost={skill.ManaCostAtRank(skills.GetRank(slot))} have={frame.GetReadOnly<Health>(heroEntity).Mana}",
      _ => block.ToString()
    };
  }

  public static int CooldownTicks(ref Frame frame, SkillAsset skill) {
    return TickMath.MsToTicksCeil(ref frame, skill?.CooldownMs ?? 0);
  }

  private static void RaiseUpgradedEvent(ref Frame frame, EntityRef entity, int playerId, int slot,
    int skillAssetId, int newRank, int remainingPoints) {
    if (frame.EventRaiser == null)
      return;

    var evt = EventPool.Get<SkillUpgradedEvent>();
    evt.UnitId = UnitLookup.GetUnitId(ref frame, entity);
    evt.PlayerId = playerId;
    evt.Slot = slot;
    evt.SkillAssetId = skillAssetId;
    evt.NewRank = newRank;
    evt.RemainingPoints = remainingPoints;
    frame.EventRaiser.RaiseEvent(evt);
  }

  private static void RaiseCastEvent(ref Frame frame, in SkillCastContext ctx, int skillAssetId) {
    if (frame.EventRaiser == null)
      return;

    var evt = EventPool.Get<SkillCastEvent>();
    evt.UnitId = UnitLookup.GetUnitId(ref frame, ctx.Caster);
    evt.PlayerId = ctx.PlayerId;
    evt.Slot = ctx.Slot;
    evt.SkillAssetId = skillAssetId;
    evt.Rank = ctx.Rank;
    evt.Position = ctx.CasterPosition;
    evt.TargetPosition = ctx.TargetPosition;
    frame.EventRaiser.RaiseEvent(evt);
  }

  private static void Reject(ref Frame frame, string action, int playerId, int slot, string reason) {
    SimLog.Info(ref frame,
      $"[Skills] event=rejected tick={frame.Tick} action={action} playerId={playerId} slot={slot} reason={reason}");
  }
}
