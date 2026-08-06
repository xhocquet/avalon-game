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
    if (!TryResolve(ref frame, playerId, slot, "Upgrade", out var heroEntity, out var heroAsset, out var skill))
      return false;

    ref var skills = ref frame.Get<SkillsComponent>(heroEntity);
    if (skills.SkillPoints <= 0) {
      Reject(ref frame, "Upgrade", playerId, slot, $"no_skill_points rank={skills.GetRank(slot)}");
      return false;
    }

    if (skills.GetRank(slot) >= skill.MaxRank) {
      Reject(ref frame, "Upgrade", playerId, slot, $"skill_at_max_rank maxRank={skill.MaxRank}");
      return false;
    }

    skills.TrySpendPoint(slot, skill.MaxRank);
    var newRank = skills.GetRank(slot);
    var remainingPoints = skills.SkillPoints;

    HeroSkillSets.Get(heroAsset.SkillSetId).OnRankGained(ref frame, heroEntity, slot, skill, newRank);
    RaiseUpgradedEvent(ref frame, heroEntity, playerId, slot, skills.GetSkillAssetId(slot), newRank,
      remainingPoints);

    frame.Logger.KInformation(
      $"[Skills] UPGRADE tick={frame.Tick} playerId={playerId} slot={slot} skillId={skills.GetSkillAssetId(slot)} rank={newRank} pointsLeft={remainingPoints}");
    return true;
  }

  // Cast a learned slot that is off cooldown.
  public static bool TryCast(ref Frame frame, int playerId, int slot) {
    if (!TryResolve(ref frame, playerId, slot, "Cast", out var heroEntity, out var heroAsset, out var skill))
      return false;

    if (!frame.Has<Health>(heroEntity) || !frame.GetReadOnly<Health>(heroEntity).IsAlive) {
      Reject(ref frame, "Cast", playerId, slot, "hero_dead");
      return false;
    }

    ref var skills = ref frame.Get<SkillsComponent>(heroEntity);
    if (skills.GetRank(slot) <= 0) {
      Reject(ref frame, "Cast", playerId, slot, "skill_not_learned");
      return false;
    }

    if (skills.GetCooldownRemainingTicks(slot) > 0) {
      Reject(ref frame, "Cast", playerId, slot,
        $"on_cooldown remainingTicks={skills.GetCooldownRemainingTicks(slot)}");
      return false;
    }

    // Started before the skill runs, so an effect that later kills or respawns its own caster cannot
    // leave the slot free.
    var cooldownTicks = CooldownTicks(ref frame, skill);
    skills.StartCooldown(slot, cooldownTicks);

    var rank = skills.GetRank(slot);
    var skillAssetId = skills.GetSkillAssetId(slot);

    HeroSkillSets.Get(heroAsset.SkillSetId).OnCast(ref frame, heroEntity, slot, skill, rank);
    RaiseCastEvent(ref frame, heroEntity, playerId, slot, skillAssetId, rank);

    frame.Logger.KInformation(
      $"[Skills] CAST tick={frame.Tick} playerId={playerId} slot={slot} skillId={skillAssetId} rank={rank} cooldownTicks={cooldownTicks}");
    return true;
  }

  // Shared front half: the player's hero, its asset row, and the SkillAsset sitting in the slot.
  private static bool TryResolve(ref Frame frame, int playerId, int slot, string action,
    out EntityRef heroEntity, out HeroAsset heroAsset, out SkillAsset skill) {
    heroAsset = null;
    skill = null;

    if (!UnitLookup.TryGetPlayerHero(ref frame, playerId, out heroEntity)) {
      Reject(ref frame, action, playerId, slot, "no_hero_for_player");
      return false;
    }

    if (!frame.Has<SkillsComponent>(heroEntity)) {
      Reject(ref frame, action, playerId, slot, "hero_missing_skills");
      return false;
    }

    var skillAssetId = frame.GetReadOnly<SkillsComponent>(heroEntity).GetSkillAssetId(slot);
    if (!frame.AssetRegistry.TryGet<SkillAsset>(skillAssetId, out skill)) {
      Reject(ref frame, action, playerId, slot, $"skill_asset_missing skillId={skillAssetId}");
      return false;
    }

    var heroAssetId = frame.GetReadOnly<Hero>(heroEntity).HeroAssetId;
    if (!frame.AssetRegistry.TryGet<HeroAsset>(heroAssetId, out heroAsset)) {
      Reject(ref frame, action, playerId, slot, $"hero_asset_missing heroId={heroAssetId}");
      return false;
    }

    return true;
  }

  // Authored milliseconds -> ticks, rounded up so a cooldown never expires early. Same conversion
  // RespawnSystem uses, including the fallback for a frame with no delta time yet.
  public static int CooldownTicks(ref Frame frame, SkillAsset skill) {
    var cooldownMs = skill?.CooldownMs ?? 0;
    if (cooldownMs <= 0)
      return 0;

    var deltaTimeMs = frame.DeltaTimeMs > 0 ? frame.DeltaTimeMs : 16;
    return (cooldownMs + deltaTimeMs - 1) / deltaTimeMs;
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

  private static void RaiseCastEvent(ref Frame frame, EntityRef entity, int playerId, int slot,
    int skillAssetId, int rank) {
    if (frame.EventRaiser == null)
      return;

    var evt = EventPool.Get<SkillCastEvent>();
    evt.UnitId = UnitLookup.GetUnitId(ref frame, entity);
    evt.PlayerId = playerId;
    evt.Slot = slot;
    evt.SkillAssetId = skillAssetId;
    evt.Rank = rank;
    evt.Position = frame.Has<TransformComponent>(entity)
      ? frame.GetReadOnly<TransformComponent>(entity).Position
      : FPVector3.Zero;
    frame.EventRaiser.RaiseEvent(evt);
  }

  private static void Reject(ref Frame frame, string action, int playerId, int slot, string reason) {
    frame.Logger.KInformation(
      $"[Skills] REJECT tick={frame.Tick} action={action} playerId={playerId} slot={slot} reason={reason}");
  }
}
