using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Logging;

namespace Meesles.Avalon.Sim.Heroes;

// The rules behind UpgradeSkillCommand and CastSkillCommand. CommandSystem dispatches straight into
// these so the command layer stays a switch and the rules can be exercised without a wire round-trip.
//
// Both entry points assume the slot index has already cleared CommandValidation - it indexes fixed
// buffers on SkillsComponent, so an unchecked value would read out of bounds.
// TODO slot references are kinda sketch
public static class SkillActions {
  // Spend one skill point to raise a slot's rank.
  public static bool TryUpgrade(ref Frame frame, int playerId, int slot) {
    var block = EvaluateUpgrade(ref frame, playerId, slot, out var heroEntity, out var heroAsset, out var skill);
    if (block != SkillBlock.None) {
      Reject(ref frame, "Upgrade", playerId, slot, Describe(ref frame, block, heroEntity, slot, skill));
      return false;
    }

    ref var skills = ref frame.Get<SkillsComponent>(heroEntity);
    skills.TrySpendPoint(slot, skill.MaxRank);
    var newRank = skills.GetRank(slot);
    var remainingPoints = skills.SkillPoints;

    HeroSkillSets.Get(heroAsset.SkillSetId).OnRankGained(ref frame, heroEntity, slot, skill, newRank);
    RaiseUpgradedEvent(ref frame, heroEntity, playerId, slot, skills.GetSkillAssetId(slot), newRank,
      remainingPoints);

    SimLog.Info(ref frame,
      $"[Skills] UPGRADE tick={frame.Tick} playerId={playerId} slot={slot} skillId={skills.GetSkillAssetId(slot)} rank={newRank} pointsLeft={remainingPoints}");
    return true;
  }

  // Cast a learned slot that is off cooldown at a planar ground point. The point is clamped to the
  // row's cast band before any effect sees it, so a client aiming past its range casts at the edge
  // rather than being rejected. Self-cast skills pass their own position and ignore it.
  public static bool TryCast(ref Frame frame, int playerId, int slot, FPVector3 target) {
    var block = EvaluateCast(ref frame, playerId, slot, out var heroEntity, out var heroAsset, out var skill);
    if (block != SkillBlock.None) {
      Reject(ref frame, "Cast", playerId, slot, Describe(ref frame, block, heroEntity, slot, skill));
      return false;
    }

    ref var skills = ref frame.Get<SkillsComponent>(heroEntity);

    // Started before the skill runs, so an effect that later kills or respawns its own caster cannot
    // leave the slot free.
    var cooldownTicks = Cheats.IsEnabled(ref frame, playerId, CheatFlags.NoCooldowns)
      ? 0
      : CooldownTicks(ref frame, skill);
    skills.StartCooldown(slot, cooldownTicks);

    var rank = skills.GetRank(slot);
    var skillAssetId = skills.GetSkillAssetId(slot);

    var casterPosition = frame.Has<TransformComponent>(heroEntity)
      ? frame.GetReadOnly<TransformComponent>(heroEntity).Position
      : FPVector3.Zero;
    // A self-cast row carries no aim, so whatever point the client sent is discarded rather than
    // clamped - the cast, its event, and any telegraph all resolve on the caster.
    target = skill.IsSelfCast
      ? casterPosition
      : SkillAim.ClampToCastRange(ref frame, heroEntity, skill, casterPosition, target);

    var ctx = new SkillCastContext(heroEntity, playerId, slot, skill, rank, casterPosition, target);
    HeroSkillSets.Get(heroAsset.SkillSetId).OnCast(ref frame, in ctx);
    RaiseCastEvent(ref frame, in ctx, skillAssetId);

    SimLog.Info(ref frame,
      $"[Skills] CAST tick={frame.Tick} playerId={playerId} slot={slot} skillId={skillAssetId} rank={rank} cooldownTicks={cooldownTicks} target=({target.x}, {target.z})");
    return true;
  }

  // Would TryCast/TryUpgrade accept this slot right now? The client asks these before it queues a
  // command, so an unlearned or cooling slot never reaches the wire and the sim never has to reject it.
  // Read-only and allocation-free: safe to call every frame off the predicted frame.
  public static bool CanCast(ref Frame frame, int playerId, int slot) {
    return EvaluateCast(ref frame, playerId, slot, out _, out _, out _) == SkillBlock.None;
  }

  // CanCast asked as if the slot had already gained pendingRanks ranks - an upgrade the client has
  // queued but the predicted frame has not run yet. Safe for the client to act on: commands drain one
  // per tick in queue order, so the upgrade always executes on an earlier tick than a cast queued
  // after it, and the sim's own re-check at arrival sees the rank.
  public static bool CanCast(ref Frame frame, int playerId, int slot, int pendingRanks) {
    return EvaluateCast(ref frame, playerId, slot, out _, out _, out _, pendingRanks) == SkillBlock.None;
  }

  public static bool CanUpgrade(ref Frame frame, int playerId, int slot) {
    return EvaluateUpgrade(ref frame, playerId, slot, out _, out _, out _) == SkillBlock.None;
  }

  // CanUpgrade asked as if pendingPoints points were already spent and the slot had already gained
  // pendingRanks ranks. Klotho schedules local input InputDelayTicks ahead, so a command the client
  // has queued is not in the predicted frame yet; without this the client would re-approve a slot it
  // has already spent its last point on and the sim would reject the second command on arrival.
  public static bool CanUpgrade(ref Frame frame, int playerId, int slot, int pendingPoints,
    int pendingRanks) {
    return EvaluateUpgrade(ref frame, playerId, slot, out _, out _, out _, pendingPoints, pendingRanks)
           == SkillBlock.None;
  }

  // The cast rules, in one place. TryCast turns a block into a reject log; the client turns it into a
  // swallowed keypress. Nothing here mutates the frame.
  private static SkillBlock EvaluateCast(ref Frame frame, int playerId, int slot,
    out EntityRef heroEntity, out HeroAsset heroAsset, out SkillAsset skill, int pendingRanks = 0) {
    var block = Resolve(ref frame, playerId, slot, out heroEntity, out heroAsset, out skill);
    if (block != SkillBlock.None)
      return block;

    if (!frame.Has<Health>(heroEntity) || !frame.GetReadOnly<Health>(heroEntity).IsAlive)
      return SkillBlock.HeroDead;

    ref readonly var skills = ref frame.GetReadOnly<SkillsComponent>(heroEntity);
    if (skills.GetRank(slot) + pendingRanks <= 0)
      return SkillBlock.NotLearned;

    return skills.GetCooldownRemainingTicks(slot) > 0 ? SkillBlock.OnCooldown : SkillBlock.None;
  }

  private static SkillBlock EvaluateUpgrade(ref Frame frame, int playerId, int slot,
    out EntityRef heroEntity, out HeroAsset heroAsset, out SkillAsset skill,
    int pendingPoints = 0, int pendingRanks = 0) {
    var block = Resolve(ref frame, playerId, slot, out heroEntity, out heroAsset, out skill);
    if (block != SkillBlock.None)
      return block;

    ref readonly var skills = ref frame.GetReadOnly<SkillsComponent>(heroEntity);
    if (skills.SkillPoints - pendingPoints <= 0)
      return SkillBlock.NoSkillPoints;

    return skills.GetRank(slot) + pendingRanks >= skill.MaxRank ? SkillBlock.AtMaxRank : SkillBlock.None;
  }

  // Shared front half: the player's hero, its asset row, and the SkillAsset sitting in the slot.
  private static SkillBlock Resolve(ref Frame frame, int playerId, int slot,
    out EntityRef heroEntity, out HeroAsset heroAsset, out SkillAsset skill) {
    heroAsset = null;
    skill = null;

    if (!UnitLookup.TryGetPlayerHero(ref frame, playerId, out heroEntity))
      return SkillBlock.NoHero;

    if (!frame.Has<SkillsComponent>(heroEntity))
      return SkillBlock.HeroMissingSkills;

    var skillAssetId = frame.GetReadOnly<SkillsComponent>(heroEntity).GetSkillAssetId(slot);
    if (!frame.AssetRegistry.TryGet<SkillAsset>(skillAssetId, out skill))
      return SkillBlock.SkillAssetMissing;

    var heroAssetId = frame.GetReadOnly<Hero>(heroEntity).HeroAssetId;
    return frame.AssetRegistry.TryGet<HeroAsset>(heroAssetId, out heroAsset)
      ? SkillBlock.None
      : SkillBlock.HeroAssetMissing;
  }

  // Block code -> the reason= text. Only walked on the reject path, so the diagnostic detail costs
  // nothing on the predicate path the client polls.
  private static string Describe(ref Frame frame, SkillBlock block, EntityRef heroEntity, int slot,
    SkillAsset skill) {
    switch (block) {
      case SkillBlock.NoHero: return "no_hero_for_player";
      case SkillBlock.HeroMissingSkills: return "hero_missing_skills";
      case SkillBlock.HeroDead: return "hero_dead";
      case SkillBlock.NotLearned: return "skill_not_learned";
      case SkillBlock.AtMaxRank: return $"skill_at_max_rank maxRank={skill.MaxRank}";
      case SkillBlock.HeroAssetMissing:
        return $"hero_asset_missing heroId={frame.GetReadOnly<Hero>(heroEntity).HeroAssetId}";
    }

    ref readonly var skills = ref frame.GetReadOnly<SkillsComponent>(heroEntity);
    return block switch {
      SkillBlock.SkillAssetMissing => $"skill_asset_missing skillId={skills.GetSkillAssetId(slot)}",
      SkillBlock.OnCooldown => $"on_cooldown remainingTicks={skills.GetCooldownRemainingTicks(slot)}",
      SkillBlock.NoSkillPoints => $"no_skill_points rank={skills.GetRank(slot)}",
      _ => block.ToString()
    };
  }

  // Why a slot cannot be cast or upgraded. A code rather than a string so the client can ask every
  // frame without allocating; Describe renders it only when the sim logs a rejection.
  private enum SkillBlock {
    None,
    NoHero,
    HeroMissingSkills,
    SkillAssetMissing,
    HeroAssetMissing,
    HeroDead,
    NotLearned,
    OnCooldown,
    NoSkillPoints,
    AtMaxRank
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
      $"[Skills] REJECT tick={frame.Tick} action={action} playerId={playerId} slot={slot} reason={reason}");
  }
}
